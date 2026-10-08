using Microsoft.EntityFrameworkCore;
using ShopSphere.Catalog.Domain;
using ShopSphere.SharedKernel;

namespace ShopSphere.Catalog.Infrastructure;
public record ProductWrite(string Name, string Description, decimal Price, string Brand, string Category, bool IsActive, int Version = 0);
public record AdminProductPage(List<Product> Items, long Total, int Page, int PageSize);
public record LowStockProduct(Product Product, int AvailableQuantity);
public record AdminCatalogSummary(long TotalProducts, long ActiveProducts, long LowStockProducts, List<LowStockProduct> LowStockItems);
public sealed class CatalogManagement(CatalogDb db)
{
    public static void ValidateList(string? search, string visibility, int page, int pageSize)
    {
        Guard.Require(page is >= 1 and <= 1_000_000 && pageSize is >= 1 and <= 100, "Page must be 1–1000000 and pageSize must be 1–100.");
        Guard.Require(search is null || search.Length <= 100, "Search must not exceed 100 characters.");
        Guard.Require(visibility is "all" or "active" or "inactive", "Visibility must be all, active or inactive.");
    }
    public async Task<AdminProductPage> List(string? search, string visibility, int page, int pageSize, CancellationToken ct)
    {
        ValidateList(search, visibility, page, pageSize);
        var query = db.Products.AsNoTracking();
        if (visibility != "all") query = query.Where(p => p.IsActive == (visibility == "active"));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, "\\") ||
                EF.Functions.ILike(p.Brand, pattern, "\\") || EF.Functions.ILike(p.Category, pattern, "\\"));
        }
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items, total, page, pageSize);
    }
    public async Task<AdminCatalogSummary> Summary(CancellationToken ct)
    {
        var total = await db.Products.LongCountAsync(ct);
        var active = await db.Products.LongCountAsync(p => p.IsActive, ct);
        var low = db.Products.AsNoTracking().Where(p => p.IsActive &&
            !db.Availability.Any(a => a.ProductId == p.Id && a.AvailableQuantity > 5));
        var lowCount = await low.LongCountAsync(ct);
        var items = await low.OrderBy(p => p.Name).ThenBy(p => p.Id).Take(10)
            .Select(p => new LowStockProduct(p,
                db.Availability.Where(a => a.ProductId == p.Id).Select(a => (int?)a.AvailableQuantity).FirstOrDefault() ?? 0))
            .ToListAsync(ct);
        return new(total, active, lowCount, items);
    }
    public async Task<Product> Find(Guid id, CancellationToken ct) => await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new ApiException(404, "Product not found.");
    public static void Validate(ProductWrite input)
    {
        Guard.Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim().Length <= 150, "Product name is required (max 150 characters).");
        Guard.Require(!string.IsNullOrWhiteSpace(input.Description) && input.Description.Length <= 5000, "Description is required (max 5,000 characters).");
        Guard.Require(!string.IsNullOrWhiteSpace(input.Brand) && input.Brand.Trim().Length <= 100, "Brand is required (max 100 characters).");
        Guard.Require(!string.IsNullOrWhiteSpace(input.Category) && input.Category.Trim().Length <= 100, "Category is required (max 100 characters).");
        Guard.Require(input.Price > 0 && input.Price <= 1_000_000 && decimal.Round(input.Price, 2) == input.Price, "Price must be positive with at most two decimal places.");
    }
    public async Task<Product> Save(Guid? id, ProductWrite input, CancellationToken ct)
    {
        Validate(input);
        var product = id.HasValue ? await Find(id.Value, ct) : new Product { Id = Guid.NewGuid(), IsActive = false };
        if (id.HasValue) CheckVersion(product, input.Version);
        product.Name = input.Name.Trim(); product.Description = input.Description.Trim(); product.Price = input.Price;
        product.Brand = input.Brand.Trim(); product.Category = input.Category.Trim(); product.IsActive = input.IsActive;
        if (!id.HasValue) db.Products.Add(product);
        else product.Version++;
        await Persist(ct);
        return product;
    }
    public static void CheckVersion(Product product, int version)
    {
        if (product.Version != version) throw new ApiException(409, "This product changed. Refresh it before saving again.");
    }
    public async Task Persist(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ApiException(409, "This product changed. Refresh it before saving again."); }
    }
}
