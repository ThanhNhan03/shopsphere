using Microsoft.EntityFrameworkCore;
using ShopSphere.Catalog.Application;
using ShopSphere.Catalog.Domain;
using ShopSphere.Messaging;
using ShopSphere.SharedKernel;

namespace ShopSphere.Catalog.Infrastructure;
public sealed class CatalogDb(DbContextOptions<CatalogDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductAvailability> Availability => Set<ProductAvailability>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        model.Entity<ProductAvailability>().HasKey(p => p.ProductId);
        model.Entity<ProductAvailability>().ToTable("ProductAvailability", t =>
            t.HasCheckConstraint("nonnegative_availability", "\"AvailableQuantity\" >= 0 AND \"Version\" >= 0"));
        model.Entity<ProductAvailability>().HasIndex(p => p.ProductId).HasDatabaseName("IX_AvailableProducts")
            .HasFilter("\"AvailableQuantity\" > 0");
        model.Entity<Product>().HasIndex(p => new { p.Name, p.Id });
        model.Entity<Product>().HasIndex(p => new { p.Price, p.Id });
        model.AddOutbox();
        model.Entity<Product>().HasData(
            Product(1, "MacBook Air M4", "A light, powerful laptop for everyday creative work. 13-inch display, 16 GB memory, 256 GB SSD.", 999m, "Apple", "Laptops", "laptop"),
            Product(2, "WH-1000XM5", "Wireless over-ear headphones with noise cancellation and up to 30 hours of battery life.", 299m, "Sony", "Audio", "headphones"),
            Product(3, "MX Master 3S", "A comfortable wireless mouse with quiet clicks, precise scrolling and USB-C charging.", 99m, "Logitech", "Accessories", "mouse"),
            Product(4, "Studio Display", "A bright 27-inch 5K display for your desk, with an integrated camera and speakers.", 1599m, "Apple", "Monitors", "monitor"),
            Product(5, "Keychron K2", "A compact wireless mechanical keyboard with a tactile typing feel and a durable aluminum frame.", 89m, "Keychron", "Accessories", "keyboard"),
            Product(6, "Portable SSD T7", "1 TB of fast, portable storage in a pocket-sized aluminum enclosure.", 109m, "Samsung", "Storage", "ssd"));
    }
    private static Product Product(int id, string name, string description, decimal price, string brand, string category, string image) =>
        new() { Id = Guid.Parse($"00000000-0000-0000-0000-{id:D12}"), Name = name, Description = description,
            Price = price, Brand = brand, Category = category, ImageUrl = $"/products/{image}.svg" };
}
public sealed class CatalogQueries(CatalogDb db, AvailabilityReadiness readiness) : ICatalog
{
    public async Task<ProductPage> List(CatalogQuery request, CancellationToken ct)
    {
        if (!readiness.Ready) throw new ApiException(503, "Product availability is updating. Please try again shortly.");
        if (request.Page is < 1 or > 1000000 || request.PageSize is < 1 or > 100)
            throw new ApiException(400, "Page must be 1–1000000 and pageSize must be 1–100.");
        if (request.Search?.Length > 100 || request.Category?.Length > 100)
            throw new ApiException(400, "Search and category must not exceed 100 characters.");
        if (request.Sort is not ("name" or "price-low" or "price-high"))
            throw new ApiException(400, "Sort must be name, price-low or price-high.");

        var query = db.Products.AsNoTracking();
        if (!request.IncludeOutOfStock)
            query = query.Where(p => db.Availability.Any(a => a.ProductId == p.Id && a.AvailableQuantity > 0));
        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(p => p.Category == request.Category.Trim());
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Treat user text as literal text rather than SQL LIKE wildcards.
            var escaped = request.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, "\\") ||
                EF.Functions.ILike(p.Brand, pattern, "\\") || EF.Functions.ILike(p.Category, pattern, "\\"));
        }
        var total = await query.LongCountAsync(ct);
        var ordered = request.Sort switch
        {
            "price-low" => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price-high" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            _ => query.OrderBy(p => p.Name).ThenBy(p => p.Id)
        };
        var items = await ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(p => new ProductView(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.Brand, p.Category,
                db.Availability.Where(a => a.ProductId == p.Id).Select(a => (int?)a.AvailableQuantity).FirstOrDefault()))
            .ToListAsync(ct);
        return new ProductPage(items, request.Page, request.PageSize, total);
    }
    public Task<Product?> Find(Guid id, CancellationToken ct) => db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
    public Task<List<string>> Categories(CancellationToken ct) => db.Products.Select(p => p.Category).Distinct().Order().ToListAsync(ct);
}
