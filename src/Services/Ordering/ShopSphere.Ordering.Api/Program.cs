using Microsoft.EntityFrameworkCore;
using ShopSphere.Messaging;
using ShopSphere.Ordering.Application;
using ShopSphere.Ordering.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("ordering");
builder.Services.AddDbContext<OrderingDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddHttpClient<IOrders, Orders>(c => c.BaseAddress = new Uri(builder.Configuration["Services:Basket"] ?? "http://localhost:5102"));
builder.Services.AddScoped<OrderEvents>();
builder.Services.AddServiceBus<OrderingDb>(builder.Configuration, "ordering", typeof(OrderEvents).Assembly);
var app = builder.Build();
app.UseDefaults();
app.MapPost("/api/orders", async (CheckoutRequest request, IOrders orders, CancellationToken ct) =>
{
    var order = await orders.Checkout(request, ct);
    return Results.Created($"/api/orders/{order.Id}", order);
});
app.MapGet("/api/orders/{id:guid}", async (Guid id, IOrders orders, CancellationToken ct) =>
    await orders.Find(id, ct) is { } order ? Results.Ok(order) : Results.NotFound());
await app.MigrateDatabase<OrderingDb>();
await app.RunAsync();
