using Microsoft.EntityFrameworkCore;
using ShopSphere.Notification.Worker;
using ShopSphere.Payment.Infrastructure;
using Xunit;

namespace ShopSphere.Tests;

public sealed class PaymentAndNotificationModelTests
{
    [Fact]
    public void PaymentReconciliationModelMatchesItsMigrations()
    {
        using var db = new PaymentDb(new DbContextOptionsBuilder<PaymentDb>()
            .UseNpgsql("Host=localhost;Database=payment_validation;Username=test;Password=test").Options);

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains(db.Model.GetEntityTypes(), type => type.ClrType == typeof(PaymentReconciliation));
    }

    [Fact]
    public void EmailDeliveryLedgerHasEventIdempotencyKeyAndCurrentMigration()
    {
        using var db = new EmailDeliveryDb(new DbContextOptionsBuilder<EmailDeliveryDb>()
            .UseNpgsql("Host=localhost;Database=notification_validation;Username=test;Password=test").Options);
        var entity = db.Model.FindEntityType(typeof(EmailDelivery))!;

        Assert.Equal(nameof(EmailDelivery.EventId), entity.FindPrimaryKey()!.Properties.Single().Name);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
