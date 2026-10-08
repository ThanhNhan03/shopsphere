using Microsoft.EntityFrameworkCore;
using ShopSphere.Messaging;
using PaymentEntity = ShopSphere.Payment.Domain.Payment;
namespace ShopSphere.Payment.Infrastructure;
public sealed class PaymentDb(DbContextOptions<PaymentDb> options) : DbContext(options)
{
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<StripeReceipt> StripeReceipts => Set<StripeReceipt>();
    public DbSet<PaymentReconciliation> Reconciliations => Set<PaymentReconciliation>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PaymentEntity>().HasIndex(p => p.OrderId).IsUnique();
        model.Entity<PaymentEntity>().Property(p => p.Amount).HasPrecision(18, 2);
        model.Entity<StripeReceipt>().HasKey(r => r.EventId);
        model.Entity<PaymentReconciliation>().HasIndex(r => new { r.OrderId, r.ReconciledAt });
        model.Entity<PaymentReconciliation>().Property(r => r.Actor).HasMaxLength(254);
        model.Entity<PaymentReconciliation>().Property(r => r.ProviderStatus).HasMaxLength(32);
        model.Entity<PaymentReconciliation>().Property(r => r.ProviderPaymentStatus).HasMaxLength(32);
        model.Entity<PaymentReconciliation>().Property(r => r.Outcome).HasMaxLength(64);
        model.Entity<PaymentReconciliation>().Property(r => r.ErrorType).HasMaxLength(160);
        model.AddOutbox();
    }
}
public sealed class PaymentReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Actor { get; set; } = "";
    public string ProviderStatus { get; set; } = "Unavailable";
    public string ProviderPaymentStatus { get; set; } = "Unavailable";
    public string Outcome { get; set; } = "";
    public string? ErrorType { get; set; }
    public DateTimeOffset ReconciledAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class StripeReceipt
{
    public string EventId { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
