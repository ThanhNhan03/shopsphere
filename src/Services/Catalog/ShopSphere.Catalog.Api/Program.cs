using Microsoft.EntityFrameworkCore;
using ShopSphere.Catalog.Application;
using ShopSphere.Catalog.Infrastructure;
using ShopSphere.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = ProductImages.MaxBytes + 65536);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = ProductImages.MaxBytes + 65536);
builder.AddDefaults("catalog");
builder.Services.AddDbContext<CatalogDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<ICatalog, CatalogQueries>();
builder.Services.AddScoped<CatalogManagement>();
builder.Services.AddSingleton<ProductImages>();
var app = builder.Build();
app.UseDefaults();
app.MapGet("/api/products", (string? category, ICatalog catalog, CancellationToken ct) => catalog.List(category, ct));
app.MapGet("/api/products/{id:guid}", async (Guid id, ICatalog catalog, CancellationToken ct) =>
    await catalog.Find(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());
app.MapGet("/api/categories", (ICatalog catalog, CancellationToken ct) => catalog.Categories(ct));
// Internal service endpoints are only reachable on the private Compose network.
app.MapGet("/internal/products/{id:guid}", async (Guid id, CatalogManagement catalog, CancellationToken ct) => Results.Ok(await catalog.Find(id, ct)));
app.MapGet("/api/admin/products", (CatalogManagement catalog, CancellationToken ct) => catalog.List(ct));
app.MapGet("/api/admin/products/{id:guid}", async (Guid id, CatalogManagement catalog, CancellationToken ct) => Results.Ok(await catalog.Find(id, ct)));
app.MapPost("/api/admin/products", async (ProductWrite input, CatalogManagement catalog, CancellationToken ct) =>
    Results.Ok(await catalog.Save(null, input, ct)));
app.MapPut("/api/admin/products/{id:guid}", async (Guid id, ProductWrite input, CatalogManagement catalog, CancellationToken ct) =>
    Results.Ok(await catalog.Save(id, input, ct)));
app.MapPost("/api/admin/products/{id:guid}/image", async (Guid id, HttpRequest request, CatalogManagement catalog, ProductImages images, ILogger<Program> log, CancellationToken ct) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { detail = "An image upload is required." });
    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("image");
    if (file is null || file.Length is <= 0 or > ProductImages.MaxBytes) return Results.BadRequest(new { detail = "Choose an image up to 5 MB." });
    var product = await catalog.Find(id, ct);
    if (!int.TryParse(form["version"], out var version)) return Results.BadRequest(new { detail = "A product version is required." });
    CatalogManagement.CheckVersion(product, version);
    using var buffer = new MemoryStream();
    await file.CopyToAsync(buffer, ct);
    var previous = product.ImageUrl;
    var uploaded = await images.Store(buffer.ToArray(), file.ContentType, ct);
    product.ImageUrl = uploaded; product.Version++;
    try { await catalog.Persist(ct); }
    catch { try { await images.Delete(uploaded, CancellationToken.None); } catch (Exception e) { log.LogWarning(e, "Could not clean an unreferenced image"); } throw; }
    try { await images.Delete(previous, ct); } catch (Exception e) { log.LogWarning(e, "Could not remove a replaced product image"); }
    return Results.Ok(product);
});
app.MapGet("/api/media/{key}", async (string key, HttpContext context, ProductImages images, CancellationToken ct) =>
{
    using var image = await images.Read(key, ct);
    context.Response.ContentType = image.Headers.ContentType;
    context.Response.ContentLength = image.ContentLength;
    context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await image.ResponseStream.CopyToAsync(context.Response.Body, ct);
});
await app.MigrateDatabase<CatalogDb>();
await app.RunAsync();
