using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MassTransit;
using MimeKit;
using ShopSphere.Contracts;

namespace ShopSphere.Notification.Worker;

public sealed class EmailDelivery
{
    public Guid EventId { get; set; }
    public Guid OrderId { get; set; }
    public string EventType { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? LastErrorType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
}

public sealed class EmailDeliveryDb(DbContextOptions<EmailDeliveryDb> options) : DbContext(options)
{
    public DbSet<EmailDelivery> Deliveries => Set<EmailDelivery>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<EmailDelivery>().HasKey(d => d.EventId);
        model.Entity<EmailDelivery>().Property(d => d.EventType).HasMaxLength(80);
        model.Entity<EmailDelivery>().Property(d => d.Status).HasMaxLength(20);
        model.Entity<EmailDelivery>().Property(d => d.LastErrorType).HasMaxLength(160);
        model.Entity<EmailDelivery>().HasIndex(d => new { d.OrderId, d.CreatedAt });
    }
}

public sealed class EmailDeliveryDbFactory : IDesignTimeDbContextFactory<EmailDeliveryDb>
{
    public EmailDeliveryDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<EmailDeliveryDb>()
        .UseNpgsql("Host=localhost;Port=6543;Database=notification_db;Username=shopsphere;Password=design-time-only").Options);
}

public sealed class CustomerEmailSender(IConfiguration configuration)
{
    public bool IsSmtp => string.Equals(configuration["Email:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase);

    public async Task SendAsync(Guid eventId, string recipient, string subject, string body, CancellationToken ct)
    {
        if (!IsSmtp) throw new InvalidOperationException("SMTP delivery is not configured.");
        var host = configuration["Email:Smtp:Host"];
        var from = configuration["Email:Smtp:FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("SMTP host and sender address must be configured.");
        var port = int.TryParse(configuration["Email:Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        if (port is < 1 or > 65535) throw new InvalidOperationException("SMTP port is invalid.");
        var security = configuration["Email:Smtp:Security"] switch
        {
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            null or "" or "StartTls" => SecureSocketOptions.StartTls,
            _ => throw new InvalidOperationException("SMTP security must be StartTls or SslOnConnect.")
        };
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configure both SMTP username and password, or neither.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(configuration["Email:Smtp:FromName"] ?? "ShopSphere", from));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.MessageId = $"{eventId:N}@shopsphere.local";
        message.Headers.Add("X-ShopSphere-Event-ID", eventId.ToString("N"));
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        client.Timeout = 15000;
        await client.ConnectAsync(host, port, security, ct);
        if (!string.IsNullOrWhiteSpace(username)) await client.AuthenticateAsync(username, password!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

public sealed class EmailDeliveryHandler(EmailDeliveryDb db, CustomerEmailSender sender, ILogger<EmailDeliveryHandler> logger)
{
    public Task Confirmed(ConsumeContext<OrderConfirmedIntegrationEvent> context) => Deliver(
        context.Message.EventId, context.Message.OrderId, nameof(OrderConfirmedIntegrationEvent), context.Message.Email,
        "Your ShopSphere order is confirmed",
        $"Thank you for your purchase. Order {context.Message.OrderId} is confirmed.", context.CancellationToken);

    public Task Cancelled(ConsumeContext<OrderCancelledIntegrationEvent> context) => Deliver(
        context.Message.EventId, context.Message.OrderId, nameof(OrderCancelledIntegrationEvent), context.Message.Email,
        "An update about your ShopSphere order",
        $"Order {context.Message.OrderId} was cancelled. Reason: {context.Message.Reason}", context.CancellationToken);

    private async Task Deliver(Guid eventId, Guid orderId, string eventType, string recipient, string subject, string body, CancellationToken ct)
    {
        var delivery = await db.Deliveries.SingleOrDefaultAsync(d => d.EventId == eventId, ct);
        if (delivery is null)
        {
            delivery = new EmailDelivery { EventId = eventId, OrderId = orderId, EventType = eventType };
            db.Deliveries.Add(delivery);
            await db.SaveChangesAsync(ct);
        }
        if (delivery.Status is "Delivered" or "Simulated") return;

        delivery.Attempts++;
        delivery.Status = "Sending";
        delivery.LastErrorType = null;
        await db.SaveChangesAsync(ct);
        if (!sender.IsSmtp)
        {
            delivery.Status = "Simulated";
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Simulated customer email for order {OrderId}, event {EventId}", orderId, eventId);
            return;
        }

        try
        {
            await sender.SendAsync(eventId, recipient, subject, body, ct);
            delivery.Status = "Delivered";
            delivery.DeliveredAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Customer email delivered for order {OrderId}, event {EventId}", orderId, eventId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            delivery.Status = "Pending";
            delivery.LastErrorType = error.GetType().Name;
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Customer email attempt {Attempt} failed for order {OrderId}, event {EventId}; error type {ErrorType}",
                delivery.Attempts, orderId, eventId, delivery.LastErrorType);
            throw;
        }
    }
}
