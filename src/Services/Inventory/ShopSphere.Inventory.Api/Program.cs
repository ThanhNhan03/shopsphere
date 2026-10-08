using Microsoft.EntityFrameworkCore;
using ShopSphere.Inventory.Application;
using ShopSphere.Inventory.Infrastructure;
using ShopSphere.Messaging;
using ShopSphere.SharedKernel;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("inventory");
builder.Services.AddDbContext<InventoryDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IInventory, InventoryQueries>();
builder.Services.AddHttpClient<InventoryManagement>(c => c.BaseAddress = new Uri(builder.Configuration["Services:Catalog"] ?? "http://localhost:5101"));
builder.Services.AddScoped<ReservationTransitions>();
builder.Services.AddServiceBus<InventoryDb>(builder.Configuration, "inventory", typeof(InventoryCreatedConsumer).Assembly);
var app = builder.Build();
app.UseDefaults();
app.MapGet("/internal/availability", async (Guid? after, int? limit, InventoryDb db, CancellationToken ct) =>
{
    var size = limit ?? 2000;
    Guard.Require(size is >= 1 and <= 2000, "Snapshot limit must be 1–2000.");
    var cursor = after ?? Guid.Empty;
    var items = await db.Stocks.FromSqlInterpolated($"SELECT * FROM \"Stocks\" WHERE \"ProductId\" > {cursor}")
        .AsNoTracking().OrderBy(s => s.ProductId).Take(size)
        .Select(s => new { s.ProductId, s.AvailableQuantity, s.Version }).ToArrayAsync(ct);
    return Results.Ok(new { Items = items, NextAfter = items.Length == size ? (Guid?)items[^1].ProductId : null });
});
app.MapGet("/api/inventory/availability", async (string ids, InventoryDb db, CancellationToken ct) =>
{
    var parts = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
    Guard.Require(parts.Length is >= 1 and <= 100 && parts.All(p => Guid.TryParse(p, out _)), "Provide 1–100 valid product IDs.");
    var products = parts.Select(Guid.Parse).Distinct().ToArray();
    var stocks = await db.Stocks.AsNoTracking().Where(s => products.Contains(s.ProductId))
        .Select(s => new { s.ProductId, s.AvailableQuantity }).ToDictionaryAsync(s => s.ProductId, ct);
    return Results.Ok(products.Select(id => new { ProductId = id, AvailableQuantity = stocks.TryGetValue(id, out var stock) ? stock.AvailableQuantity : 0 }));
});
app.MapGet("/api/inventory/{id:guid}", async (Guid id, IInventory inventory, CancellationToken ct) =>
    await inventory.Find(id, ct) is { } stock ? Results.Ok(stock) : Results.NotFound());
app.MapGet("/api/admin/inventory", (string ids, InventoryManagement inventory, CancellationToken ct) => inventory.List(ids, ct));
app.MapGet("/api/admin/inventory/{id:guid}/adjustments", (Guid id, InventoryManagement inventory, CancellationToken ct) => inventory.History(id, ct));
app.MapPost("/api/admin/inventory/{id:guid}/adjustments", async (Guid id, AdjustmentWrite input, HttpContext context, InventoryManagement inventory, CancellationToken ct) =>
    Results.Ok(await inventory.Adjust(id, input, context.Request.Headers["X-Admin-Email"].ToString(), ct)));
await app.MigrateDatabase<InventoryDb>();
await app.RunAsync();
