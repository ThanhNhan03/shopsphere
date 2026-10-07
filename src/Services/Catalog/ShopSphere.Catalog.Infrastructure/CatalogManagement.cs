using Microsoft.EntityFrameworkCore;
using ShopSphere.Catalog.Domain;
using ShopSphere.SharedKernel;

namespace ShopSphere.Catalog.Infrastructure;
public record ProductWrite(string Name, string Description, decimal Price, string Brand, string Category, bool IsActive, int Version = 0);
public sealed class CatalogManagement(CatalogDb db)
{
    public Task<List<Product>> List(CancellationToken ct) => db.Products.AsNoTracking().OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToListAsync(ct);
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
