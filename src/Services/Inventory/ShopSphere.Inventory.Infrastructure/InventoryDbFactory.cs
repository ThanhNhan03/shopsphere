using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ShopSphere.Inventory.Infrastructure;
public sealed class InventoryDbFactory : IDesignTimeDbContextFactory<InventoryDb>
{
    public InventoryDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<InventoryDb>()
        .UseNpgsql("Host=localhost;Port=6543;Database=inventory_db;Username=shopsphere;Password=design-time-only")
        .Options);
}
