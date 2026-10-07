# Order flow

The browser creates a stable CheckoutId for submission/retry and sends only customer details. Ordering reads current basket items, validates names/email/quantities, calculates the total, and commits Order + OrderCreated outbox messages.

OrderCreated → Inventory → InventoryReserved → Ordering: AwaitingPayment.
The InventoryReserved state is represented by its integration event; the order immediately becomes AwaitingPayment once reservation completes.

Payment also consumes InventoryReserved and persists a pending payment. Next.js polls until payment exists. Demo buttons settle through explicit simulation endpoints. Stripe mode creates the hosted Session only when the customer continues to pay.

PaymentCompleted → Ordering: Confirmed → OrderConfirmed → Inventory commit, Basket cleanup and simulated Notification.
PaymentFailed → Ordering: Cancelled → InventoryReleaseRequested → Inventory release; OrderCancelled → Notification.
InventoryReservationFailed → Ordering: Cancelled; no release is needed because all-or-nothing reservation changed no stock.

Order and payment terminal states are immutable. Late reservation events cannot reopen a terminal order. Repeated success/failure events do not publish another transition. Inbox deduplication is complemented by business record guards.

A customer who leaves before starting payment has 30 minutes; the Payment expiry worker emits PaymentFailed for stale Pending payments. Once a Stripe Session is started, its signed expiry webhook ends the reservation. Cancel redirects preserve the active Session so checkout can be resumed.

Error queues contain exhausted retries; inspect RabbitMQ and Seq to diagnose failed processing. This demo does not implement automated error-queue replay or Stripe reconciliation.

Check scripts/smoke-test.mjs for a runnable happy path, failure path, concurrent retry and insufficient-stock test. Each run consumes one unit of the SSD catalog product.
