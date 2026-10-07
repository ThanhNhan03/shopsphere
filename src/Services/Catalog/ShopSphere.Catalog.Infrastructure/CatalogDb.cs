using Microsoft.EntityFrameworkCore;
using ShopSphere.Catalog.Application;
using ShopSphere.Catalog.Domain;

namespace ShopSphere.Catalog.Infrastructure;
public sealed class CatalogDb(DbContextOptions<CatalogDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        model.Entity<Product>().Property(p => p.Version).IsConcurrencyToken();
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
public sealed class CatalogQueries(CatalogDb db) : ICatalog
{
    public Task<List<Product>> List(string? category, CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => p.IsActive && (category == null || p.Category == category)).OrderBy(p => p.Name).ToListAsync(ct);
    public Task<Product?> Find(Guid id, CancellationToken ct) => db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.IsActive, ct);
    public Task<List<string>> Categories(CancellationToken ct) => db.Products.Where(p => p.IsActive).Select(p => p.Category).Distinct().Order().ToListAsync(ct);
}
