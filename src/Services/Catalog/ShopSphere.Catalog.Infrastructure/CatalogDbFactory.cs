using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ShopSphere.Catalog.Infrastructure;
public sealed class CatalogDbFactory : IDesignTimeDbContextFactory<CatalogDb>
{
    public CatalogDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<CatalogDb>()
        .UseNpgsql("Host=localhost;Port=6543;Database=catalog_db;Username=shopsphere;Password=design-time-only")
        .Options);
}
