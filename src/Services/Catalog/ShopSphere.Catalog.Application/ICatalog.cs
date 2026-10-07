using ShopSphere.Catalog.Domain;
namespace ShopSphere.Catalog.Application;
public interface ICatalog
{
    Task<List<Product>> List(string? category, CancellationToken ct);
    Task<Product?> Find(Guid id, CancellationToken ct);
    Task<List<string>> Categories(CancellationToken ct);
}
