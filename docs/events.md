# Integration events

Contracts live in contracts/ShopSphere.Contracts/Events.cs. Each event includes EventId, CorrelationId and OccurredAt. CorrelationId is the order ID; EventId identifies one publication.

| Event | Publisher | Consumers |
| --- | --- | --- |
| OrderCreatedIntegrationEvent | Ordering | Inventory |
| InventoryReservedIntegrationEvent | Inventory | Ordering, Payment |
| InventoryReservationFailedIntegrationEvent | Inventory | Ordering |
| PaymentCompletedIntegrationEvent | Payment | Ordering |
| PaymentFailedIntegrationEvent | Payment | Ordering |
| InventoryReleaseRequestedIntegrationEvent | Ordering | Inventory |
| OrderConfirmedIntegrationEvent | Ordering | Inventory, Basket, Notification |
| OrderCancelledIntegrationEvent | Ordering | Notification |

MassTransit adds its transport MessageId and persists duplicate detection per receive endpoint. Logical retries are also guarded by CheckoutId, reservation OrderId, payment OrderId and persistent StripeEventId.

Do not place Stripe keys, raw card details or unrelated customer data in events. Email is carried only where Payment or the simulated Notification needs it. Confirmed events include customer/basket snapshot information for Redis cleanup.
