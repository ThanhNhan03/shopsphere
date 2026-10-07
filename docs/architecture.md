# Architecture

Next.js uses a server-side same-origin API proxy to YARP. Gateway routes products/categories, basket, orders, inventory and payments to their owners. Services are only reachable within the Compose network; Gateway and the frontend publish loopback ports.

Catalog, Ordering, Inventory and Payment own separate PostgreSQL databases on one instance. Redis holds baskets. Service databases are never queried by another service.

Each major service has Domain, Application, Infrastructure and Api projects. Basket needs no domain project for its quantity hash. Two building blocks contain boundary errors/validation and shared hosting, logging and MassTransit configuration; contracts carry integration events.

MassTransit **8.5.11** is pinned intentionally. Version 9 introduces commercial licensing; the project uses the open-source v8 line. See the [official repository](https://github.com/MassTransit/MassTransit) and [outbox documentation](https://masstransit.io/documentation/configuration/middleware/outbox).

Every stateful event consumer runs within the EF consumer outbox transaction. API producers publish through the EF bus outbox before SaveChanges/commit. Consumers save state and outgoing messages in the same database; broker delivery is retried independently. Different events for the same order/payment lock the same row. Checkout and reservation creation use transaction-scoped PostgreSQL advisory locks for concurrent duplicate requests.

Inventory locks product rows in ascending ID order, checks every line before changing any stock, and persists one reservation per order. Stock constraints prevent negative quantities. Confirmation reduces reserved quantity without returning available stock; failure releases once.

Stripe event receipts are permanent records keyed by Stripe event ID. Signature verification occurs before database writes. Supported Session events must match the stored Session ID, currency, integer amount, order reference and test mode. Completed events must have payment_status=paid. Stripe API creation uses a stable idempotency key based on the order ID. See [Stripe fulfillment guidance](https://docs.stripe.com/checkout/fulfillment).

Basket confirmation uses one Redis Lua operation for a duplicate marker and purchased-quantity subtraction, preserving quantities added after checkout. Markers and baskets expire after seven days; this is a local demo retention policy, not a permanent audit ledger.

PostgreSQL 18 stores data in a versioned directory under /var/lib/postgresql, so the named volume is mounted at that parent directory. See the [official image documentation](https://hub.docker.com/_/postgres). Seq is local and explicitly configured without authentication; its port binds only to loopback.

No real email is sent. A delivery ledger and provider idempotency would be needed before replacing simulated log output with email.
