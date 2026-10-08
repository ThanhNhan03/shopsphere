# ShopSphere

A technology e-commerce demo with .NET 10 microservices, Clean Architecture, Next.js, PostgreSQL, Redis, RabbitMQ/MassTransit, Stripe Checkout, YARP, MinIO/S3 product images, and Serilog/Seq.

## Run locally

Install Docker Desktop with Linux containers, then:

```powershell
Copy-Item .env.example .env
./scripts/setup-local-admin.ps1
docker compose up -d --build
```

Open [the store](http://localhost:3000). Compose creates five databases (including Gateway accounts), applies committed EF Core migrations and seeds six products with 20 units each on a fresh database. First startup downloads and builds the images. Keep an existing `.env` instead of copying over it.

Register at `/register` or sign in at `/login` with email and password. Sign-in is required for the bag, checkout, orders and payments; browsing remains public. Google/Gmail is an optional alternative: configure `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` as described in [Google sign-in setup](docs/google-login.md).

Open `/admin` with the seeded `admin@shopsphere.local` account. The setup script generates a strong random password and saves the account details in Git-ignored `.local/admin-account.txt`. Admin can create/edit/show/hide products, upload photos, adjust stock with reasons/history, and review orders and confirmed revenue. See [administration and storage](docs/admin.md).

For the optional million-product dataset, resumable generation, bounded product API and read-only verification, see [Catalog sample data](docs/catalog-dataset.md). The regular EF seed still creates only the original six products; large data is loaded explicitly with `scripts/seed-catalog.ps1`.

| Component | Local address |
| --- | --- |
| Store | http://localhost:3000 |
| API gateway | http://localhost:8080 |
| Administration | http://localhost:3000/admin |
| MinIO console | http://localhost:9001 |
| MinIO S3 API | http://localhost:9000 |
| RabbitMQ management | http://localhost:15672 |
| Seq logs | http://localhost:8081 |
| PostgreSQL | localhost:6543 |
| Redis | localhost:6379 |

Local demo credentials are in `.env.example`. Exposed ports bind to loopback. Change host ports in `.env` if needed; when changing the frontend port also update `FRONTEND_URL`. Containers reach PostgreSQL at `postgres:5432`, independently of the host port.

```powershell
docker compose ps
docker compose logs -f ordering-api inventory-api payment-api notification-worker
docker compose down
```

`down` preserves named data volumes. Do not remove volumes unless you deliberately want to delete the demo data. Recreating a container does not reset stock.

## Payment modes

**Demo** is the default to let the entire flow run without external credentials. Add products, open the bag, enter checkout details, then use **Simulate successful payment** or **Simulate payment failure** on the order page. No card is charged; the UI labels this mode explicitly.

**Stripe** connects to real Stripe infrastructure in test mode:

1. Set `PAYMENT_MODE=Stripe` and `STRIPE_SECRET_KEY=sk_test_...` in the ignored `.env`.
2. Run `stripe listen --forward-to http://localhost:8080/api/payments/webhooks/stripe` using the [Stripe CLI](https://docs.stripe.com/stripe-cli).
3. Set `STRIPE_WEBHOOK_SECRET` to the `whsec_...` value printed by the listener.
4. Run `docker compose up -d payment-api` to recreate Payment with the new settings.
5. Create a fresh order, continue to Stripe, and use [Stripe test payment details](https://docs.stripe.com/testing).

Only signed, matching test-mode Checkout Session webhooks can settle Stripe payments. The browser redirect merely opens the order page, which polls the backend. Secret keys stay in Payment; no publishable key is needed for a hosted Checkout redirect. Demo simulation is disabled in Stripe mode.

Closing/cancelling Stripe Checkout does not immediately cancel the order: payment can be resumed until the session expires (31 minutes). The `checkout.session.expired` webhook cancels the order and releases stock. Orders whose payment is never started expire after 30 minutes. Keep the webhook listener running.

## Checkout behavior

1. Catalog serves products and prices from `catalog_db`.
2. Basket stores product quantities in Redis and reads current product information over Catalog HTTP and available stock over Inventory HTTP. Adding/increasing a line checks its cumulative quantity against current stock.
3. Ordering reads the refreshed basket over HTTP, rejects known stock shortages before creating an order, validates details and calculates the amount on the server.
4. Ordering atomically stores the order and `OrderCreatedIntegrationEvent` through the EF transactional bus outbox.
5. Inventory locks stock rows in product-ID order, reserves all items atomically and emits a reservation result.
6. Payment creates a pending payment from the reservation event; the checkout endpoint creates an idempotent Stripe Session on demand.
7. A successful payment confirms the order; Inventory commits reserved stock, Basket removes purchased quantities, and Notification logs a simulated email.
8. Payment failure cancels the order and emits a release request; Inventory returns reserved stock and Basket remains available for retry.

`CheckoutId` is the order ID and retry key. Repeating the same checkout concurrently returns the original order; reusing that ID with different customer details returns an error. Prices supplied by the browser are ignored.

Basket lines expose `availableQuantity`, and the basket exposes `canCheckout`. Known shortages return HTTP 409; unavailable Inventory checks return HTTP 503. Bag checks do not reserve stock: another buyer can acquire the last units before the final locked Inventory reservation, which may still reject the order.

The catalog lists available products by default; **Include out of stock** explicitly shows sold-out items with purchasing disabled. Availability is projected from Inventory events before pagination and checked live in one batch per visible page. Existing bag quantities count toward the add limit. See [catalog availability](docs/catalog-availability.md) for synchronization, contracts and checks.

MassTransit provides persistent bus/consumer outboxes, transactional inbox processing, a seven-day duplicate detection window, retries, and error queues. Domain transitions and reservation records also protect against repeated business operations. Stripe event IDs are retained durably. See [architecture](docs/architecture.md), [order flow](docs/order-flow.md), and [event contracts](docs/events.md).

## Structure

```text
ShopSphere.sln
Directory.Build.props / Directory.Packages.props
Dockerfile                       # shared .NET multi-stage build
docker-compose.yml / .env.example
src/
  Gateway/ShopSphere.Gateway
  Services/
    Catalog/                     # Api, Application, Domain, Infrastructure
    Basket/                      # Api, Application, Infrastructure
    Ordering/                    # Api, Application, Domain, Infrastructure
    Inventory/                   # Api, Application, Domain, Infrastructure
    Payment/                     # Api, Application, Domain, Infrastructure
  Workers/ShopSphere.Notification.Worker
  BuildingBlocks/
    ShopSphere.SharedKernel      # boundary validation/errors
    ShopSphere.Messaging         # hosting/logging/outbox setup
contracts/ShopSphere.Contracts
frontend/shopsphere-web           # Next.js App Router, React Query, Zod, Tailwind
tests/ShopSphere.Tests
infra/postgres/create-databases.sh
scripts/
docs/
```

Domain does not reference Infrastructure. Application defines service operations. Infrastructure implements persistence, HTTP clients, consumers and Stripe. APIs map requests to those operations. Each service owns its database; cross-service access uses APIs or events.

The original design and team/day plan are preserved in [project brief](docs/project-brief.md). The runtime uses two practical simplifications: no separate EventBus wrapper over MassTransit, and the Notification worker is a small hosted process with a health endpoint.

See the [feature completion plan](docs/feature-roadmap.md) for the remaining Stripe verification and release checklist.

## Develop and verify

Requires .NET SDK 10 and Node.js 24 for commands outside Docker:

```powershell
dotnet restore ShopSphere.sln
dotnet test ShopSphere.sln -c Release
cd frontend/shopsphere-web
npm ci
npm run lint
npm run typecheck
npm run build
```

To run Next.js against the Compose gateway during UI development, copy `frontend/shopsphere-web/.env.example` to `.env.local` and run `npm run dev -- --port 3001`. The server-side proxy reads `GATEWAY_URL` at runtime; the browser always calls same-origin `/api`.

For migration changes:

```powershell
dotnet tool restore
dotnet ef migrations add YourChange --project src/Services/Ordering/ShopSphere.Ordering.Infrastructure --startup-project src/Services/Ordering/ShopSphere.Ordering.Api --output-dir Persistence/Migrations
```

Substitute the owning service. Design-time factories create migration models without connecting to a database; runtime configuration comes from Compose. Committed migrations run at startup, suitable for this single-instance demo. Coordinate migrations separately before deploying multiple instances.

With the stack running in Demo mode:

```powershell
node --env-file=.env scripts/admin-smoke-test.mjs
```

The authenticated smoke check exercises registration/login/logout, administrator authorization, MinIO image replacement, visibility, concurrent edit protection, stock adjustment history, concurrent checkout retries and order confirmation. It uses an isolated customer/product/order, then hides the test product and clears its available stock. The test account and order remain in history. The original anonymous `smoke-test.mjs` and webhook harness need authentication updates; do not disable Gateway authorization to run them.

For the generated dataset, also run:

```powershell
node scripts/basket-stock-test.mjs
node scripts/verify-availability-projection.mjs
```

This stock check requires the optional generated dataset. It uses internal Compose services and isolated basket keys, verifies cumulative/concurrent stock caps and rejection before order creation, then exercises reservation/release with a cancelled Demo QA order. It leaves that order as test evidence, removes its basket keys and verifies natural stock compensation; it does not reset stock or change user bags. Gateway authentication remains enabled.

The older baseline smoke harness is also available:

```powershell
node scripts/smoke-test.mjs
```

The baseline smoke check exercises real HTTP, PostgreSQL, Redis and RabbitMQ, including concurrent checkout retries, duplicate settlement, insufficient inventory and compensation. It consumes one SSD per successful run and uses isolated customer IDs. Its anonymous Gateway calls predate mandatory sign-in and require an authenticated harness before use with the current configuration.

Signed webhook fixtures can be checked without a Stripe account:

```powershell
docker compose run -d --no-deps --name shopsphere-webhook-test -p 127.0.0.1:5105:8080 -e Payment__Mode=Stripe -e Stripe__WebhookSecret=whsec_fixture_only payment-api
node scripts/stripe-webhook-test.mjs
docker rm -f shopsphere-webhook-test
```

This checks signature rejection, amount matching, duplicate webhook persistence, successful confirmation and failed-payment compensation using synthetic Sessions. It does **not** verify external Stripe Session creation or a real Stripe payment.

GitHub Actions runs backend tests, Compose validation, frontend lint and frontend build.

## Scope and limitations

Email/password and optional Google sign-in plus Gateway ownership checks protect the storefront's basket, orders and payment operations. Gateway enforces administrator access and replaces client-supplied audit identity headers. Internal services must remain private to the Docker network. Fulfillment, shipping, tax, reviews, promotions and email delivery remain outside scope. Notification may log duplicate messages after redelivery; it sends no actual email. Email verification, password recovery and account/role administration are not implemented.

Stock is held while awaiting payment. Started Stripe sessions rely on webhooks to expire; webhook delivery/reconciliation must be monitored before a real deployment. Demo checkouts can be settled directly from the order page.

Production frontend dependencies currently pass `npm audit --omit=dev`. The Next.js lint toolchain includes an upstream `braces` advisory without a patched release at implementation time; this is a development dependency, tracked in [validation notes](docs/validation.md).
