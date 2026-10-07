using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Contracts;
using ShopSphere.Ordering.Domain;
namespace ShopSphere.Ordering.Infrastructure;

// All transitions for an order lock the same row, even when different event endpoints run concurrently.
public sealed class OrderEvents(OrderingDb db, IPublishEndpoint publish)
{
    private async Task<Order> Lock(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\" = {id} FOR UPDATE").SingleAsync(ct);
        await db.Entry(order).Collection(o => o.Items).LoadAsync(ct);
        return order;
    }
    public async Task Reserve(Guid id, CancellationToken ct)
    {
        var order = await Lock(id, ct);
        if (order.Reserve()) await db.SaveChangesAsync(ct);
    }
    public async Task Confirm(Guid id, CancellationToken ct)
    {
        var order = await Lock(id, ct);
        if (!order.Confirm()) return;
        await publish.Publish(new OrderConfirmedIntegrationEvent(Guid.NewGuid(), id, DateTimeOffset.UtcNow,
            id, order.Email, order.CustomerId, order.Items.Select(i => new OrderLine(i.ProductId, i.Name, i.UnitPrice, i.Quantity)).ToArray()), ct);
        await db.SaveChangesAsync(ct);
    }
    public async Task Cancel(Guid id, string reason, bool release, CancellationToken ct)
    {
        var order = await Lock(id, ct);
        if (!order.Cancel(reason)) return;
        await publish.Publish(new OrderCancelledIntegrationEvent(Guid.NewGuid(), id, DateTimeOffset.UtcNow, id, order.Email, reason), ct);
        if (release) await publish.Publish(new InventoryReleaseRequestedIntegrationEvent(Guid.NewGuid(), id, DateTimeOffset.UtcNow, id), ct);
        await db.SaveChangesAsync(ct);
    }
}
public sealed class OrderReservedConsumer(OrderEvents events) : IConsumer<InventoryReservedIntegrationEvent>
{
    public Task Consume(ConsumeContext<InventoryReservedIntegrationEvent> c) => events.Reserve(c.Message.OrderId, c.CancellationToken);
}
public sealed class OrderInventoryFailedConsumer(OrderEvents events) : IConsumer<InventoryReservationFailedIntegrationEvent>
{
    public Task Consume(ConsumeContext<InventoryReservationFailedIntegrationEvent> c) => events.Cancel(c.Message.OrderId, c.Message.Reason, false, c.CancellationToken);
}
public sealed class OrderPaymentCompletedConsumer(OrderEvents events) : IConsumer<PaymentCompletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> c) => events.Confirm(c.Message.OrderId, c.CancellationToken);
}
public sealed class OrderPaymentFailedConsumer(OrderEvents events) : IConsumer<PaymentFailedIntegrationEvent>
{
    public Task Consume(ConsumeContext<PaymentFailedIntegrationEvent> c) => events.Cancel(c.Message.OrderId, c.Message.Reason, true, c.CancellationToken);
}
