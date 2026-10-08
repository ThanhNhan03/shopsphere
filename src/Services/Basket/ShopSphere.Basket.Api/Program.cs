using MassTransit;
using ShopSphere.Basket.Application;
using ShopSphere.Basket.Infrastructure;
using ShopSphere.Messaging;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("basket");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.AddHttpClient<IBaskets, RedisBaskets>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101"));
builder.Services.AddHttpClient<IStockAvailability, InventoryAvailability>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:Inventory"] ?? "http://localhost:5104");
    c.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BasketConfirmedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "shopsphere");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "shopsphere-local");
        });
        cfg.UseMessageRetry(r => r.Intervals(200, 1000, 3000));
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("basket", false));
    });
});
var app = builder.Build();
app.UseDefaults();
app.MapGet("/api/basket/{customer}", (string customer, IBaskets baskets, CancellationToken ct) => baskets.Get(customer, ct));
app.MapPost("/api/basket/{customer}/items", (string customer, AddItemRequest request, IBaskets baskets, CancellationToken ct) =>
    baskets.Add(customer, request, ct));
app.MapPut("/api/basket/{customer}/items/{product:guid}", (string customer, Guid product, QuantityRequest request, IBaskets baskets, CancellationToken ct) =>
    baskets.Update(customer, product, request.Quantity, ct));
app.MapDelete("/api/basket/{customer}/items/{product:guid}", async (string customer, Guid product, IBaskets baskets) =>
{
    await baskets.Remove(customer, product);
    return Results.NoContent();
});
app.MapDelete("/api/basket/{customer}", async (string customer, IBaskets baskets) =>
{
    await baskets.Remove(customer, null);
    return Results.NoContent();
});
await app.RunAsync();
