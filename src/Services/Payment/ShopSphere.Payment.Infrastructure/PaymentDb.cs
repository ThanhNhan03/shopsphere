using Microsoft.EntityFrameworkCore;
using ShopSphere.Messaging;
using PaymentEntity = ShopSphere.Payment.Domain.Payment;
namespace ShopSphere.Payment.Infrastructure;
public sealed class PaymentDb(DbContextOptions<PaymentDb> options) : DbContext(options)
{
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<StripeReceipt> StripeReceipts => Set<StripeReceipt>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PaymentEntity>().HasIndex(p => p.OrderId).IsUnique();
        model.Entity<PaymentEntity>().Property(p => p.Amount).HasPrecision(18, 2);
        model.Entity<StripeReceipt>().HasKey(r => r.EventId);
        model.AddOutbox();
    }
}
public sealed class StripeReceipt
{
    public string EventId { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
