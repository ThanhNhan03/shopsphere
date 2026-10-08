using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Contracts;
using ShopSphere.Inventory.Domain;
namespace ShopSphere.Inventory.Infrastructure;
public sealed class InventoryCreatedConsumer(InventoryDb db, IPublishEndpoint publish) : IConsumer<OrderCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var m = context.Message;
        var ct = context.CancellationToken;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({m.OrderId.ToString()}))", ct);
        if (await db.Reservations.AnyAsync(r => r.OrderId == m.OrderId, ct)) return;
        var lines = m.Items.GroupBy(i => i.ProductId).Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) }).OrderBy(i => i.ProductId).ToArray();
        var stocks = new Dictionary<Guid, Stock>();
        foreach (var item in lines)
        {
            var stock = await db.Stocks.FromSqlInterpolated($"SELECT * FROM \"Stocks\" WHERE \"ProductId\" = {item.ProductId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (stock != null) stocks[item.ProductId] = stock;
        }
        var reservation = new Reservation { OrderId = m.OrderId };
        db.Reservations.Add(reservation);
        if (lines.Length == 0 || lines.Any(i => i.Quantity <= 0 || !stocks.TryGetValue(i.ProductId, out var stock) || stock.AvailableQuantity < i.Quantity))
        {
            reservation.Status = "Rejected";
            await publish.Publish(new InventoryReservationFailedIntegrationEvent(Guid.NewGuid(), m.CorrelationId, DateTimeOffset.UtcNow, m.OrderId, "Insufficient inventory."), ct);
        }
        else
        {
            foreach (var item in lines)
            {
                stocks[item.ProductId].Reserve(item.Quantity);
                var updated = stocks[item.ProductId];
                await publish.Publish(new StockAvailabilityChangedIntegrationEvent(Guid.NewGuid(), m.CorrelationId,
                    DateTimeOffset.UtcNow, updated.ProductId, updated.AvailableQuantity, updated.Version), ct);
                reservation.Items.Add(new() { OrderId = m.OrderId, ProductId = item.ProductId, Quantity = item.Quantity });
            }
            await publish.Publish(new InventoryReservedIntegrationEvent(Guid.NewGuid(), m.CorrelationId, DateTimeOffset.UtcNow, m.OrderId, m.Email, m.TotalAmount, m.Items), ct);
        }
        await db.SaveChangesAsync(ct);
    }
}
public sealed class ReservationTransitions(InventoryDb db, IPublishEndpoint publish)
{
    public async Task Apply(Guid orderId, bool commit, CancellationToken ct)
    {
        var reservation = await db.Reservations.FromSqlInterpolated($"SELECT * FROM \"Reservations\" WHERE \"OrderId\" = {orderId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (reservation is null || reservation.Status != "Reserved") return;
        await db.Entry(reservation).Collection(r => r.Items).LoadAsync(ct);
        foreach (var item in reservation.Items.OrderBy(i => i.ProductId))
        {
            var stock = await db.Stocks.FromSqlInterpolated($"SELECT * FROM \"Stocks\" WHERE \"ProductId\" = {item.ProductId} FOR UPDATE").SingleAsync(ct);
            if (commit) stock.Commit(item.Quantity); else stock.Release(item.Quantity);
            await publish.Publish(new StockAvailabilityChangedIntegrationEvent(Guid.NewGuid(), orderId,
                DateTimeOffset.UtcNow, stock.ProductId, stock.AvailableQuantity, stock.Version), ct);
        }
        reservation.Status = commit ? "Committed" : "Released";
        await db.SaveChangesAsync(ct);
    }
}
public sealed class InventoryReleaseConsumer(ReservationTransitions transitions) : IConsumer<InventoryReleaseRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<InventoryReleaseRequestedIntegrationEvent> c) => transitions.Apply(c.Message.OrderId, false, c.CancellationToken);
}
public sealed class InventoryConfirmedConsumer(ReservationTransitions transitions) : IConsumer<OrderConfirmedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderConfirmedIntegrationEvent> c) => transitions.Apply(c.Message.OrderId, true, c.CancellationToken);
}
