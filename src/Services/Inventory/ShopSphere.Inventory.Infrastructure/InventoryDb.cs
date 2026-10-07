using Microsoft.EntityFrameworkCore;
using ShopSphere.Inventory.Application;
using ShopSphere.Inventory.Domain;
using ShopSphere.Messaging;
namespace ShopSphere.Inventory.Infrastructure;
public sealed class InventoryDb(DbContextOptions<InventoryDb> options) : DbContext(options)
{
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Stock>().HasKey(s => s.ProductId);
        model.Entity<Stock>().Property(s => s.Version).HasDefaultValue(0L);
        model.Entity<Stock>().ToTable(t => t.HasCheckConstraint("nonnegative_stock", "\"AvailableQuantity\" >= 0 AND \"ReservedQuantity\" >= 0"));
        model.Entity<Stock>().HasData(Enumerable.Range(1, 6).Select(i => new Stock
        { ProductId = Guid.Parse($"00000000-0000-0000-0000-{i:D12}"), AvailableQuantity = 20 }));
        model.Entity<Reservation>().HasKey(r => r.OrderId);
        model.Entity<Reservation>().HasMany(r => r.Items).WithOne().HasForeignKey(i => i.OrderId);
        model.Entity<ReservationItem>().HasKey(i => new { i.OrderId, i.ProductId });
        model.AddOutbox();
    }
}
public sealed class InventoryQueries(InventoryDb db) : IInventory
{
    public Task<StockView?> Find(Guid product, CancellationToken ct) => db.Stocks.AsNoTracking().Where(s => s.ProductId == product)
        .Select(s => new StockView(s.ProductId, s.AvailableQuantity, s.ReservedQuantity)).SingleOrDefaultAsync(ct);
}
