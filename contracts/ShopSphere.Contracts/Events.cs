namespace ShopSphere.Contracts;

public record StockAvailabilityChangedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt,
    Guid ProductId, int AvailableQuantity, long Version);

public record OrderLine(Guid ProductId, string Name, decimal UnitPrice, int Quantity);
public record OrderCreatedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt,
    Guid OrderId, string CustomerId, string Email, decimal TotalAmount, OrderLine[] Items);
public record InventoryReservedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt,
    Guid OrderId, string Email, decimal TotalAmount, OrderLine[] Items);
public record InventoryReservationFailedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt,
    Guid OrderId, string Reason);
public record PaymentCompletedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt, Guid OrderId);
public record PaymentFailedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt, Guid OrderId, string Reason);
public record InventoryReleaseRequestedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt, Guid OrderId);
public record OrderConfirmedIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt,
    Guid OrderId, string Email, string CustomerId, OrderLine[] Items);
public record OrderCancelledIntegrationEvent(Guid EventId, Guid CorrelationId, DateTimeOffset OccurredAt,
    Guid OrderId, string Email, string Reason);
