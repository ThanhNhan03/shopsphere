using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ShopSphere.Ordering.Infrastructure;
public sealed class OrderingDbFactory : IDesignTimeDbContextFactory<OrderingDb>
{
    public OrderingDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<OrderingDb>()
        .UseNpgsql("Host=localhost;Port=6543;Database=ordering_db;Username=shopsphere;Password=design-time-only")
        .Options);
}
