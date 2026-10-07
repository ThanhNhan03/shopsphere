using Microsoft.EntityFrameworkCore;
using ShopSphere.Messaging;
using ShopSphere.Ordering.Domain;
namespace ShopSphere.Ordering.Infrastructure;
public sealed class OrderingDb(DbContextOptions<OrderingDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Order>().Property(o => o.TotalAmount).HasPrecision(18, 2);
        model.Entity<Order>().HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        model.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
        model.AddOutbox();
    }
}
