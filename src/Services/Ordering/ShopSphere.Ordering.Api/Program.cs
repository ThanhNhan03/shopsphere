using Microsoft.EntityFrameworkCore;
using ShopSphere.Messaging;
using ShopSphere.Ordering.Application;
using ShopSphere.Ordering.Infrastructure;
using ShopSphere.Ordering.Domain;
using ShopSphere.SharedKernel;
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
app.MapGet("/api/admin/orders/summary", async (OrderingDb db, CancellationToken ct) => new
{
    totalOrders = await db.Orders.CountAsync(ct),
    pendingOrders = await db.Orders.CountAsync(o => o.Status != OrderStatus.Confirmed && o.Status != OrderStatus.Cancelled, ct),
    confirmedOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Confirmed, ct),
    cancelledOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled, ct),
    revenue = await db.Orders.Where(o => o.Status == OrderStatus.Confirmed).SumAsync(o => o.TotalAmount, ct)
});
app.MapGet("/api/admin/orders", async (string? status, string? q, int? page, OrderingDb db, CancellationToken ct) =>
{
    var index = page ?? 1; Guard.Require(index >= 1 && index <= 100_000, "Invalid page.");
    var query = db.Orders.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(status))
    {
        Guard.Require(Enum.TryParse<OrderStatus>(status, out var state) && Enum.IsDefined(state), "Invalid order status.");
        query = query.Where(o => o.Status == state);
    }
    if (!string.IsNullOrWhiteSpace(q))
    {
        Guard.Require(q.Length <= 100, "Search must be at most 100 characters.");
        var term = q.Trim();
        query = query.Where(o => o.CustomerName.Contains(term) || o.Email.Contains(term) || o.Id.ToString().Contains(term));
    }
    var total = await query.CountAsync(ct);
    var items = await query.Include(o => o.Items).OrderByDescending(o => o.CreatedAt).Skip((index - 1) * 20).Take(20).ToListAsync(ct);
    return Results.Ok(new { items, total, page = index, pageSize = 20 });
});
app.MapGet("/api/admin/orders/{id:guid}", async (Guid id, IOrders orders, CancellationToken ct) =>
    await orders.Find(id, ct) is { } order ? Results.Ok(order) : Results.NotFound());
await app.MigrateDatabase<OrderingDb>();
await app.RunAsync();
