using Microsoft.EntityFrameworkCore;
using ShopSphere.Inventory.Application;
using ShopSphere.Inventory.Infrastructure;
using ShopSphere.Messaging;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("inventory");
builder.Services.AddDbContext<InventoryDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IInventory, InventoryQueries>();
builder.Services.AddScoped<ReservationTransitions>();
builder.Services.AddServiceBus<InventoryDb>(builder.Configuration, "inventory", typeof(InventoryCreatedConsumer).Assembly);
var app = builder.Build();
app.UseDefaults();
app.MapGet("/api/inventory/{id:guid}", async (Guid id, IInventory inventory, CancellationToken ct) =>
    await inventory.Find(id, ct) is { } stock ? Results.Ok(stock) : Results.NotFound());
await app.MigrateDatabase<InventoryDb>();
await app.RunAsync();
