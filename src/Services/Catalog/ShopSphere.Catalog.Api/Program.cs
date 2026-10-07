using Microsoft.EntityFrameworkCore;
using ShopSphere.Catalog.Application;
using ShopSphere.Catalog.Infrastructure;
using ShopSphere.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("catalog");
builder.Services.AddDbContext<CatalogDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<ICatalog, CatalogQueries>();
var app = builder.Build();
app.UseDefaults();
app.MapGet("/api/products", (string? category, ICatalog catalog, CancellationToken ct) => catalog.List(category, ct));
app.MapGet("/api/products/{id:guid}", async (Guid id, ICatalog catalog, CancellationToken ct) =>
    await catalog.Find(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());
app.MapGet("/api/categories", (ICatalog catalog, CancellationToken ct) => catalog.Categories(ct));
await app.MigrateDatabase<CatalogDb>();
await app.RunAsync();
