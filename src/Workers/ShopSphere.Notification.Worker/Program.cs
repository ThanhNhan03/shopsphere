using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Contracts;
using ShopSphere.Messaging;
using ShopSphere.Notification.Worker;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("notification");
builder.Services.AddDbContext<EmailDeliveryDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<CustomerEmailSender>();
builder.Services.AddScoped<EmailDeliveryHandler>();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ConfirmedNotification>();
    x.AddConsumer<CancelledNotification>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureRabbitMqHost(builder.Configuration);
        cfg.UseMessageRetry(r => r.Intervals(200, 1000, 3000));
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("notification", false));
    });
});
var app = builder.Build();
app.UseDefaults();
await app.MigrateDatabase<EmailDeliveryDb>();
await app.RunAsync();

public sealed class ConfirmedNotification(EmailDeliveryHandler delivery) : IConsumer<OrderConfirmedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderConfirmedIntegrationEvent> c) => delivery.Confirmed(c);
}
public sealed class CancelledNotification(EmailDeliveryHandler delivery) : IConsumer<OrderCancelledIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> c) => delivery.Cancelled(c);
}
