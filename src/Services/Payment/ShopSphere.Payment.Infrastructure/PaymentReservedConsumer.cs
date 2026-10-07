using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Contracts;
using PaymentEntity = ShopSphere.Payment.Domain.Payment;
namespace ShopSphere.Payment.Infrastructure;
public sealed class PaymentReservedConsumer(PaymentDb db) : IConsumer<InventoryReservedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryReservedIntegrationEvent> context)
    {
        var m = context.Message;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({m.OrderId.ToString()}))", context.CancellationToken);
        if (await db.Payments.AnyAsync(p => p.OrderId == m.OrderId, context.CancellationToken)) return;
        db.Payments.Add(new PaymentEntity { OrderId = m.OrderId, Amount = m.TotalAmount, Email = m.Email });
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
