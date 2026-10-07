using Microsoft.EntityFrameworkCore;
using ShopSphere.Inventory.Application;
using ShopSphere.Inventory.Infrastructure;
using ShopSphere.Messaging;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("inventory");
builder.Services.AddDbContext<InventoryDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IInventory, InventoryQueries>();
builder.Services.AddHttpClient<InventoryManagement>(c => c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101"));
builder.Services.AddScoped<ReservationTransitions>();
builder.Services.AddServiceBus<InventoryDb>(builder.Configuration, "inventory", typeof(InventoryCreatedConsumer).Assembly);
var app = builder.Build();
app.UseDefaults();
app.MapGet("/api/inventory/{id:guid}", async (Guid id, IInventory inventory, CancellationToken ct) =>
    await inventory.Find(id, ct) is { } stock ? Results.Ok(stock) : Results.NotFound());
app.MapGet("/api/admin/inventory", (InventoryManagement inventory, CancellationToken ct) => inventory.List(ct));
app.MapGet("/api/admin/inventory/{id:guid}/adjustments", (Guid id, InventoryManagement inventory, CancellationToken ct) => inventory.History(id, ct));
app.MapPost("/api/admin/inventory/{id:guid}/adjustments", async (Guid id, AdjustmentWrite input, HttpContext context, InventoryManagement inventory, CancellationToken ct) =>
    Results.Ok(await inventory.Adjust(id, input, context.Request.Headers["X-Admin-Email"].ToString(), ct)));
await app.MigrateDatabase<InventoryDb>();
await app.RunAsync();
