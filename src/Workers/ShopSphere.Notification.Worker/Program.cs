using MassTransit;
using ShopSphere.Contracts;
using ShopSphere.Messaging;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("notification");
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ConfirmedNotification>();
    x.AddConsumer<CancelledNotification>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "shopsphere");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "shopsphere-local");
        });
        cfg.UseMessageRetry(r => r.Intervals(200, 1000, 3000));
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("notification", false));
    });
});
var app = builder.Build();
app.UseDefaults();
await app.RunAsync();

public sealed class ConfirmedNotification(ILogger<ConfirmedNotification> logger) : IConsumer<OrderConfirmedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderConfirmedIntegrationEvent> c)
    {
        // ponytail: logging simulates delivery; add a durable delivery ledger before sending real email.
        logger.LogInformation("SIMULATED EMAIL: Order {OrderId} confirmed for {Email}; correlation {CorrelationId}", c.Message.OrderId, c.Message.Email, c.Message.CorrelationId);
        return Task.CompletedTask;
    }
}
public sealed class CancelledNotification(ILogger<CancelledNotification> logger) : IConsumer<OrderCancelledIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> c)
    {
        logger.LogInformation("SIMULATED EMAIL: Order {OrderId} cancelled for {Email}: {Reason}; correlation {CorrelationId}",
            c.Message.OrderId, c.Message.Email, c.Message.Reason, c.Message.CorrelationId);
        return Task.CompletedTask;
    }
}
