# ShopSphere

ShopSphere is an online store for technology products, built with .NET 10 microservices and a Next.js storefront. Its services use PostgreSQL, Redis, RabbitMQ/MassTransit, Stripe Checkout, YARP, MinIO/S3-compatible object storage, and Serilog with Seq.

## Features

- Public product catalog with search, availability, product details, and stock-aware purchase controls.
- Customer registration and email/password sign-in; optional Google sign-in when configured.
- Account-backed shopping bag, checkout, order history, and order details.
- Administrator tools for product management, product image uploads, inventory adjustments with audit history, order review, and payment reconciliation.
- Inventory reservations and order/payment updates coordinated with asynchronous events and transactional outbox/inbox processing.
- Hosted Stripe Checkout, signed webhook verification, payment retries, session expiry handling, and reconciliation.
- Customer order email delivery through SMTP, with a local console delivery option.

## Architecture

The storefront sends browser requests through its same-origin `/api` proxy to the API gateway. The gateway handles authentication, authorization, request routing, rate limiting, and administrator identity headers. Each business service owns its data and communicates with other services through HTTP APIs or RabbitMQ events.

| Component | Responsibility |
| --- | --- |
| Storefront | Product browsing, customer accounts, bag, checkout, order pages, and administration UI. |
| Gateway | YARP routing, customer accounts and authentication, access checks, and request policies. |
| Catalog | Product data, product administration, image metadata, and availability projection. |
| Basket | Customer baskets stored in Redis and current product/stock checks. |
| Ordering | Order creation, checkout validation, order state, and order event handling. |
| Inventory | Stock levels, reservations, releases, and administrator stock adjustments. |
| Payment | Payment records, Stripe Checkout sessions, signed webhook processing, and reconciliation. |
| Notification worker | Customer email delivery and delivery ledger. |
| Infrastructure | PostgreSQL, Redis, RabbitMQ, MinIO, and Seq. |

## Run locally

Requirements: Docker Desktop with Linux containers. For development commands outside Docker, install .NET SDK 10 and Node.js 24.

From the repository root, create local configuration and start the services:

```powershell
Copy-Item .env.example .env
./scripts/setup-local-admin.ps1
docker compose up -d --build
```

The setup script creates a strong random password for `admin@shopsphere.local` and writes the credentials to the Git-ignored `.local/admin-account.txt`. Keep `.env` and that account file private. Compose initializes six PostgreSQL databases, runs committed EF Core migrations, and seeds the initial catalog on a fresh database. The first startup downloads and builds container images. Keep an existing `.env` when updating the project rather than replacing it with the template.

Open the storefront at [http://localhost:3000](http://localhost:3000). Create a customer account at `/register` or sign in at `/login`. Sign-in is required for the bag, checkout, orders, and payment operations; product browsing is public. Google sign-in is optional; configure `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` using [Google sign-in setup](docs/google-login.md).

Open `/admin` and use the generated administrator credentials. Administrators can create, edit, show, or hide products; upload product images; adjust stock with a reason and review its history; review orders; and reconcile payments. See [administration and storage](docs/admin.md).

| Service | Local address |
| --- | --- |
| Storefront | [http://localhost:3000](http://localhost:3000) |
| API gateway | [http://localhost:8080](http://localhost:8080) |
| Administration | [http://localhost:3000/admin](http://localhost:3000/admin) |
| MinIO console | [http://localhost:9001](http://localhost:9001) |
| MinIO S3 API | [http://localhost:9000](http://localhost:9000) |
| RabbitMQ management | [http://localhost:15672](http://localhost:15672) |
| Seq logs | [http://localhost:8081](http://localhost:8081) |
| PostgreSQL | `localhost:6543` |
| Redis | `localhost:6379` |

Credentials for local services are documented in `.env.example`. Compose publishes host ports on loopback. Change host ports in `.env` when needed; if changing the storefront port, also update `FRONTEND_URL`. Containers connect to PostgreSQL at `postgres:5432`, regardless of the host port.

Useful Compose commands:

```powershell
docker compose ps
docker compose logs -f ordering-api inventory-api payment-api notification-worker
docker compose down
```

`docker compose down` preserves named data volumes. Remove volumes only when you intend to delete local database, basket, message-broker, or object-storage data.

## Payment

The local payment simulator supports the complete order flow without contacting Stripe. On an order page it provides controls to simulate a successful or failed payment; no card is charged.

Stripe Checkout can be configured in test mode for end-to-end payment verification:

1. Put your Stripe test secret key (`sk_test_...`) in the ignored `.env` file as `STRIPE_SECRET_KEY`.
2. Install the official [Stripe CLI](https://docs.stripe.com/cli/install).
3. From the repository root, run `./scripts/start-stripe-local.ps1`. The script captures the webhook signing secret, enables Stripe mode, and recreates the Payment service using the configured local ports.
4. Create an order, continue to hosted Stripe Checkout, and use [Stripe test payment details](https://docs.stripe.com/testing).

The Stripe CLI listener must remain active while testing webhooks. Run the script again after restarting Windows or changing the key; pass `-Stop` to stop its listener. See the [Stripe runbook](docs/stripe.md) for verification steps and configuration details.

Only signed Checkout Session webhooks with matching payment details can settle Stripe payments. The browser redirect opens the order page, which reads payment status from the backend. Secret keys remain in the Payment service; hosted Checkout does not require a publishable key. The local simulator is unavailable when Stripe mode is selected. Administrators can reconcile recent Stripe sessions from `/admin/payments`. Live payment operations require live credentials, a matching HTTPS webhook endpoint, and operational verification; they are not established by local test-mode checks. See [payment operations](docs/payment-operations.md).

Closing a Stripe Checkout page does not immediately cancel the order: payment can be resumed until the session expires. The `checkout.session.expired` webhook cancels the order and releases stock. Orders whose payment is never started expire after 30 minutes. Keep the webhook listener running during local Stripe tests.

## Checkout and inventory flow

1. Catalog returns product data and prices from `catalog_db`.
2. Basket stores customer quantities in Redis and reads current product information from Catalog and available stock from Inventory. Adding or increasing a line checks the requested total against current stock.
3. Ordering refreshes the basket, checks known shortages, validates checkout details, and calculates the amount on the server. Prices provided by the browser are ignored.
4. Ordering stores the order and its `OrderCreatedIntegrationEvent` atomically through the transactional bus outbox.
5. Inventory locks stock rows in product-ID order, reserves all order items atomically, and publishes the reservation result.
6. Payment creates a pending payment from the reservation event. Its checkout endpoint creates an idempotent Stripe Session when Stripe is configured.
7. A successful payment confirms the order. Inventory commits the reserved stock, Basket removes purchased quantities, and Notification sends the customer email through configured SMTP or the local console provider.
8. A failed payment cancels the order and requests stock release. Inventory returns the reserved stock and the basket remains available for another attempt.

`CheckoutId` is the order ID and retry key. Concurrent retries with the same checkout details return the original order; reusing the ID with different customer details returns an error. Basket lines expose `availableQuantity`, and the basket exposes `canCheckout`. Known stock shortages return HTTP 409; unavailable Inventory checks return HTTP 503. Bag checks do not reserve stock, so a final locked Inventory reservation can reject an order if another buyer acquired the remaining stock.

Catalog lists available products by default. **Include out of stock** shows sold-out products with purchasing disabled. Availability is projected from Inventory events before pagination and checked live in one batch per visible page. Existing basket quantities count toward the add limit. See [catalog availability](docs/catalog-availability.md).

MassTransit configures persistent bus and consumer outboxes, transactional inbox processing, a seven-day duplicate-detection window, retries, and error queues. Domain transitions and reservation records protect against repeated business operations. Stripe event IDs are retained durably. See [architecture](docs/architecture.md), [order flow](docs/order-flow.md), and [event contracts](docs/events.md).

## Project structure

```text
.
├── .github/
│   └── workflows/ci.yml                 # Build, test, Compose, and frontend checks
├── contracts/
│   └── ShopSphere.Contracts/            # Integration event contracts
├── docs/                                # Architecture, setup, operations, and feature notes
├── frontend/
│   └── shopsphere-web/                  # Next.js storefront and administration UI
│       ├── public/                      # Fonts and local product artwork
│       └── src/
│           ├── app/
│           │   ├── admin/[[...section]]/ # Admin dashboard sections
│           │   ├── api/[...path]/       # Same-origin gateway proxy
│           │   ├── cart/                # Customer bag
│           │   ├── checkout/            # Checkout details and payment handoff
│           │   ├── login/               # Sign-in
│           │   ├── orders/[id]/         # Order details and payment status
│           │   ├── payment/success/      # Stripe return page
│           │   ├── products/[id]/        # Product details
│           │   ├── register/             # Customer registration
│           │   └── page.tsx              # Storefront/catalog
│           ├── components/              # Shared storefront components
│           ├── features/                # Admin, auth, basket, catalog, checkout, orders
│           ├── lib/                     # API client and shared utilities
│           └── types/                   # Frontend types
├── infra/
│   ├── minio/Dockerfile                 # MinIO image configuration
│   └── postgres/create-databases.sh     # Creates service databases
├── scripts/                             # Local admin, Stripe, smoke, and verification tools
├── src/
│   ├── BuildingBlocks/
│   │   ├── ShopSphere.Messaging/        # Service setup, logging, MassTransit/outbox wiring
│   │   └── ShopSphere.SharedKernel/     # Shared API errors and boundary validation
│   ├── Gateway/ShopSphere.Gateway/      # YARP, accounts, auth, policies, account migrations
│   ├── Services/
│   │   ├── Basket/
│   │   │   ├── ShopSphere.Basket.Api/
│   │   │   ├── ShopSphere.Basket.Application/
│   │   │   └── ShopSphere.Basket.Infrastructure/  # Redis, availability, event consumers
│   │   ├── Catalog/
│   │   │   ├── ShopSphere.Catalog.Api/
│   │   │   ├── ShopSphere.Catalog.Application/
│   │   │   ├── ShopSphere.Catalog.Domain/
│   │   │   └── ShopSphere.Catalog.Infrastructure/ # PostgreSQL, MinIO, availability projection
│   │   ├── Inventory/
│   │   │   ├── ShopSphere.Inventory.Api/
│   │   │   ├── ShopSphere.Inventory.Application/
│   │   │   ├── ShopSphere.Inventory.Domain/
│   │   │   └── ShopSphere.Inventory.Infrastructure/ # Stock, reservations, adjustments, migrations
│   │   ├── Ordering/
│   │   │   ├── ShopSphere.Ordering.Api/
│   │   │   ├── ShopSphere.Ordering.Application/
│   │   │   ├── ShopSphere.Ordering.Domain/
│   │   │   └── ShopSphere.Ordering.Infrastructure/ # Orders, persistence, event consumers
│   │   └── Payment/
│   │       ├── ShopSphere.Payment.Api/
│   │       ├── ShopSphere.Payment.Application/
│   │       ├── ShopSphere.Payment.Domain/
│   │       └── ShopSphere.Payment.Infrastructure/  # Stripe, persistence, expiry, reconciliation
│   └── Workers/
│       └── ShopSphere.Notification.Worker/ # Email providers and delivery ledger
├── tests/
│   └── ShopSphere.Tests/                # Backend unit and model tests
├── outputs/
│   └── shopsphere-feature-task-breakdown-20261008/
│       └── shopsphere-feature-task-breakdown.xlsx  # Project task breakdown
├── .env.example                         # Local configuration template
├── .gitignore                           # Excludes secrets, build output, and local files
├── .dockerignore                        # Files excluded from container build context
├── docker-compose.yml                   # Storefront, APIs, worker, and infrastructure
├── Dockerfile                           # Shared .NET multi-stage build
├── Directory.Build.props                # Shared .NET build settings
├── Directory.Packages.props             # Central NuGet package versions
├── global.json                          # .NET SDK selection
└── ShopSphere.sln                       # .NET solution
```

The `scripts/` directory contains the local administrator setup (`setup-local-admin.ps1`), Stripe listener setup (`start-stripe-local.ps1`), the authenticated admin/checkout check (`admin-smoke-test.mjs`), the legacy baseline smoke harness (`smoke-test.mjs`), Stripe checkout and webhook checks (`stripe-checkout-test.mjs`, `stripe-webhook-test.mjs`), and the Catalog availability projection check (`verify-availability-projection.mjs`).

The frontend uses the Next.js App Router. Its main pages are the storefront, product detail, cart, checkout, login, registration, order detail, payment return, and `/admin` sections. `src/app/api/[...path]` proxies browser requests to the gateway. Frontend dependencies include React, TypeScript, Tailwind CSS, TanStack Query, and Zod.

The .NET business services follow Clean Architecture: `Domain` contains business models and rules, `Application` defines service operations, `Infrastructure` implements persistence and integrations, and `Api` exposes HTTP endpoints. Basket has no separate Domain project. Each service owns its database and migrations; Basket data lives in Redis. Integration messages are defined in `contracts/ShopSphere.Contracts`.

The Compose stack includes `frontend`, `gateway`, `catalog-api`, `basket-api`, `ordering-api`, `inventory-api`, `payment-api`, `notification-worker`, `postgres-init`, `postgres`, `redis`, `rabbitmq`, `seq`, and `minio`.

### Documentation

| File | Contents |
| --- | --- |
| [docs/admin.md](docs/admin.md) | Administration, product images, and MinIO/S3 storage. |
| [docs/architecture.md](docs/architecture.md) | Service boundaries and architectural decisions. |
| [docs/catalog-availability.md](docs/catalog-availability.md) | Stock projection and availability behavior. |
| [docs/deployment-security.md](docs/deployment-security.md) | Deployment configuration and security requirements. |
| [docs/email-delivery.md](docs/email-delivery.md) | SMTP and local email delivery configuration. |
| [docs/events.md](docs/events.md) | Integration event contracts and processing. |
| [docs/feature-roadmap.md](docs/feature-roadmap.md) | Feature status and remaining release work. |
| [docs/google-login.md](docs/google-login.md) | Google sign-in configuration. |
| [docs/learning-project-plan.md](docs/learning-project-plan.md) | Learning and implementation plan. |
| [docs/order-flow.md](docs/order-flow.md) | Checkout, order, and inventory flow. |
| [docs/payment-operations.md](docs/payment-operations.md) | Payment reconciliation and operating procedures. |
| [docs/project-brief.md](docs/project-brief.md) | Project goals and initial brief. |
| [docs/stripe.md](docs/stripe.md) | Stripe Checkout setup and verification. |
| [docs/ui-design.md](docs/ui-design.md) | Storefront and administration UI notes. |
| [docs/validation.md](docs/validation.md) | Validation and dependency check notes. |

## Develop and verify

Restore and test the .NET solution:

```powershell
dotnet restore ShopSphere.sln
dotnet test ShopSphere.sln -c Release
```

Check the frontend:

```powershell
cd frontend/shopsphere-web
npm ci
npm run lint
npm run typecheck
npm run build
```

To run Next.js locally against the Compose gateway, copy `frontend/shopsphere-web/.env.example` to `.env.local`, then start the development server on port 3001:

```powershell
npm run dev -- --port 3001
```

The server-side proxy reads `GATEWAY_URL` at runtime; the browser calls same-origin `/api`.

For a migration change, restore local .NET tools and target the Infrastructure project owned by the service. For example:

```powershell
dotnet tool restore
dotnet ef migrations add YourChange --project src/Services/Ordering/ShopSphere.Ordering.Infrastructure --startup-project src/Services/Ordering/ShopSphere.Ordering.Api --output-dir Persistence/Migrations
```

Use the corresponding service projects for other migrations. Design-time factories create migration models without connecting to a database. Committed migrations run at service startup for the current single-instance Compose topology; coordinate migrations separately before deploying multiple instances.

With the Compose services running and the local payment simulator selected, run the authenticated administration and checkout smoke check:

```powershell
node --env-file=.env scripts/admin-smoke-test.mjs
```

It exercises registration, login/logout, administrator authorization, MinIO image replacement, product visibility, concurrent edit protection, stock adjustment history, concurrent checkout retries, and order confirmation. It creates an isolated customer, product, and order, then hides the test product and clears its available stock. The account and order remain in history. The anonymous `scripts/smoke-test.mjs` predates mandatory sign-in and requires an authenticated harness before it can run with the current Gateway authorization. For Stripe Sessions and CLI-delivered events, use `scripts/stripe-checkout-test.mjs` as described in the [Stripe runbook](docs/stripe.md).

To check that Catalog ignores delayed or duplicate stock events, run this against a local stack with RabbitMQ management and Catalog available:

```powershell
node scripts/verify-availability-projection.mjs
```

This check publishes isolated stock events through RabbitMQ and removes its temporary catalog projection row. It does not create products, orders, payments, or change a customer's basket.

Signed webhook fixtures can be tested without contacting Stripe:

```powershell
docker compose run -d --no-deps --name shopsphere-webhook-test -p 127.0.0.1:5105:8080 -e Payment__Mode=Stripe -e Stripe__WebhookSecret=whsec_fixture_only payment-api
node --env-file=.env scripts/stripe-webhook-test.mjs
docker rm -f shopsphere-webhook-test
```

The fixture check covers signature rejection, amount matching, duplicate webhook persistence, successful confirmation, and failed-payment compensation using synthetic Sessions. It does not verify external Stripe Session creation or a real Stripe payment. GitHub Actions runs backend tests, Compose validation, frontend lint, and frontend build.

## Scope and operational notes

Email/password and optional Google sign-in, plus Gateway ownership checks, protect customer baskets, orders, and payment operations. Verified Google identities are persisted in Gateway accounts. Gateway enforces administrator access and replaces client-supplied audit identity headers. Internal service endpoints are intended to remain on the Docker network.

Fulfillment, shipping, tax, reviews, and promotions are outside the current feature set. Email verification, password recovery, and account/role administration are not implemented. SMTP delivery is at-least-once around provider/database crash boundaries. Production startup validates required secret and TLS settings, but a production deployment and live payment operation have not been verified; see [deployment security](docs/deployment-security.md).

Stock remains held while payment is pending. Started Stripe Sessions rely on webhook expiry handling, so webhook delivery and reconciliation need monitoring before live operations.

Production frontend dependencies currently pass `npm audit --omit=dev`. The frontend lint toolchain includes an upstream `braces` advisory without a patched release at the time of the latest validation; this development dependency is tracked in [validation notes](docs/validation.md).
