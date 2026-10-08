using ShopSphere.Catalog.Domain;
namespace ShopSphere.Catalog.Application;
public interface ICatalog
{
    Task<ProductPage> List(CatalogQuery query, CancellationToken ct);
    Task<Product?> Find(Guid id, CancellationToken ct);
    Task<List<string>> Categories(CancellationToken ct);
}

public sealed record CatalogQuery(string? Category, string? Search, string Sort, int Page, int PageSize, bool IncludeOutOfStock = false);
public sealed record ProductView(Guid Id, string Name, string Description, decimal Price, string ImageUrl,
    string Brand, string Category, int? AvailableQuantity);
public sealed record ProductPage(List<ProductView> Items, int Page, int PageSize, long TotalCount)
{
    public long TotalPages => (TotalCount + PageSize - 1) / PageSize;
}
