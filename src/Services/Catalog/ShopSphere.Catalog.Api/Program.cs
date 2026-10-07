using Microsoft.EntityFrameworkCore;
using ShopSphere.Catalog.Application;
using ShopSphere.Catalog.Infrastructure;
using ShopSphere.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("catalog");
builder.Services.AddDbContext<CatalogDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<ICatalog, CatalogQueries>();
builder.Services.AddSingleton<AvailabilityReadiness>();
builder.Services.AddHttpClient<AvailabilitySnapshotClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:Inventory"] ?? "http://localhost:5104");
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHostedService<AvailabilitySynchronizer>();
builder.Services.AddServiceBus<CatalogDb>(builder.Configuration, "catalog", typeof(AvailabilityChangedConsumer).Assembly);
var app = builder.Build();
app.UseDefaults();
app.MapGet("/api/products", (string? category, string? q, string? sort, int? page, int? pageSize, bool? includeOutOfStock,
    ICatalog catalog, CancellationToken ct) =>
    catalog.List(new CatalogQuery(category, q, sort ?? "name", page ?? 1, pageSize ?? 24, includeOutOfStock ?? false), ct));
app.MapGet("/api/products/{id:guid}", async (Guid id, ICatalog catalog, CancellationToken ct) =>
    await catalog.Find(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());
app.MapGet("/api/categories", (ICatalog catalog, CancellationToken ct) => catalog.Categories(ct));
await app.MigrateDatabase<CatalogDb>();
await app.RunAsync();
