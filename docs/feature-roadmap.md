# ShopSphere feature completion plan

**Last reviewed:** 2026-10-08

**Scope update:** The subsequent administration request adds local email/password registration/login, a seeded administrator, product/stock/order management, and MinIO photo storage. See [the admin runbook](admin.md) and use `node --env-file=.env scripts/admin-smoke-test.mjs` for authenticated verification. Google/Gmail remains an optional login provider with Gateway ownership checks. The older anonymous baseline and day plan below are retained as the original checkout plan, not the current implementation scope.
**Source of truth:** `README.md` and `docs/project-brief.md`
**Target:** complete and demonstrate the README's checkout and the requested administration/account features. Fulfillment, shipping, tax, reviews, promotions and email delivery remain outside this target.

## Current status

The codebase and Docker stack implement both **Demo** and **Stripe test mode** checkout. On 2026-10-08, real Stripe Session creation, CLI-delivered signed completion/expiry events, storefront confirmation, stock/basket effects and repeat-event handling passed. See [Stripe setup](stripe.md) and [validation evidence](validation.md). The local stack is currently configured for Stripe; repository defaults remain Demo.

- [x] Catalog: six seeded products, categories and product details.
- [x] Basket: Redis storage, quantity changes and product information fetched from Catalog.
- [x] Order creation: server-side prices, validation and repeat-safe checkout ID.
- [x] Inventory: atomic reservation, overselling protection, commit on success and release on failure.
- [x] Messaging: RabbitMQ/MassTransit, transactional outbox/inbox and retry.
- [x] Payment service: hosted Checkout Session integration code, signed webhook validation, persistent event receipts and Demo simulation.
- [x] Customer flow: catalog, product detail, bag, checkout and order-status pages.
- [x] Notification worker: confirmation/cancellation events appear in logs as simulated emails.
- [x] Initial EF migrations, Docker Compose, CI workflow, smoke checks and synthetic signed-webhook checks.
- [x] External Stripe Test Mode checkout verified with test credentials and a running Stripe CLI listener.
- [x] Local registration/login, seeded admin, administration and MinIO product images implemented; see [admin runbook](admin.md).

The local Stripe payment gate is passed. Remaining release work includes a fresh-stack/team rehearsal, broker-outage recovery and a successful GitHub Actions run for the final commit. The browser return URL alone is not proof of payment.

## Three-day team plan

This follows the three-person roles and three-day order in the project brief. The first day's demo flow is already implemented and checked locally; use that time to confirm the team can reproduce it from the instructions.

### Day 1 — Reproduce the baseline and prepare Stripe

**Developer 1 — Commerce and frontend**
- [x] Walk through browsing, product detail, adding/removing items, quantity changes and server-calculated totals.
- [ ] Check the workflow from a new browser session; record any confusing loading, empty-bag or error states.
- [ ] Confirm the local ports and environment instructions work on each developer's machine.

**Developer 2 — Orders and inventory**
- [x] Walk through order creation, inventory reservation, confirmation, cancellation, stock release and concurrent checkout attempts.
- [ ] Inspect RabbitMQ and Seq while running one success and one failure; record any unhandled error queues.
- [ ] Verify each teammate can start the stack without deleting the shared data volumes.

**Developer 3 — Payment and release**
- [x] Configure Stripe test-mode credentials in the developer's ignored `.env`.
- [x] Install the official Stripe CLI and confirm its listener reaches the local webhook endpoint.
- [x] Use isolated test customers/products and record a repeatable verification run.

**Day 1 exit criteria:** every teammate can run the Demo journey; the Stripe test key and webhook listener are ready for Day 2.

### Day 2 — Prove the distributed payment flow

**Developer 3 — Stripe**
- [x] Configure Stripe credentials and the listener's signing secret locally; recreate Payment.
- [x] Create an isolated order through the authenticated API, continue from its storefront order page to hosted Checkout, and confirm the signed `checkout.session.completed` event confirms the order after a test payment.
- [x] Verify backend order/payment state, the UI, stock commit and basket cleanup. RabbitMQ/Seq manual inspection remains in the release checklist below.
- [x] Reject invalid signatures and acknowledge repeated signed events without a second business transition.
- [x] Expire a real Session and confirm cancellation, stock release and retained basket.

**Developer 2 — Order processing**
- [x] The synthetic webhook check covers invalid signatures, an incorrect amount, repeated success/expiry events and expiry compensation.
- [x] Review real Stripe test-mode Sessions against the synthetic checks: references, currency, amounts and terminal-state behavior passed.
- [ ] Confirm consumers recover after RabbitMQ is briefly unavailable, with failed messages visible in the broker.

**Developer 1 — Storefront**
- [x] Verify hosted Checkout redirect and the return page showing the backend-confirmed order; confirmation is webhook-driven.
- [x] Keep secret keys in the backend and show Stripe controls in Stripe mode; Demo simulation is rejected.
- [ ] Capture any usability issues from the presenter and prioritize only blockers for the buying journey.

**Day 2 exit criteria:** one test-mode Stripe payment confirms an order through the signed webhook; a failure or expiry releases stock; no payment path trusts the browser redirect.

### Day 3 — Release rehearsal

**Developer 1 — Demo experience**
- [ ] Start from the documented setup and rehearse the demo in order: product → bag → checkout → payment → order confirmed.
- [ ] Rehearse the failure path and explain the inventory release and retained basket.
- [ ] Keep the UI limited to the README scope. Do not add admin, promotions or other stretch features during this day.

**Developer 2 — Reliability and CI**
- [x] Run backend Release build, 33 unit tests, frontend lint/TypeScript/production Docker build and Compose validation locally.
- [ ] Run `node scripts/smoke-test.mjs` on a fresh stack; confirm it exits successfully without relying on previous test data.
- [ ] Verify the GitHub Actions run for the release commit is green. The current workflow builds/tests .NET, validates Compose, and lints/builds the frontend; the full Docker smoke test remains a local check.
- [ ] Review RabbitMQ error queues and Seq after the rehearsal. Write down any known limitation rather than claiming it is covered.

**Developer 3 — Payment and project handoff**
- [ ] Rehearse Stripe from the storefront using test mode and keep the webhook listener running for the presentation.
- [x] Ensure Stripe keys/local state are ignored by Git and document switching back to Demo.
- [x] Update README and add the repeatable Stripe runbook; preserve the project brief.

**Day 3 exit criteria:** a clean-stack demo, a successful Stripe test payment, a demonstrated compensation path, passing CI, and a short runbook the team can follow.

## Follow-up after the core demo

The remaining items below concern shared/public deployment. Authentication and local administration were added by subsequent user requests.

### P1 — Before a shared or public deployment

- [x] Add authentication and authorize basket, payment and order lookups against the signed-in customer at Gateway.
- [ ] Replace local demo credentials, open Seq configuration and unauthenticated Redis/RabbitMQ connections with deployment secrets and appropriate access controls.
- [ ] Add production TLS, environment-specific Stripe webhook secrets, request rate limits and secret/PII redaction.
- [ ] Monitor Stripe webhook delivery and RabbitMQ error queues; define operator steps to replay or reconcile a payment safely.
- [ ] Replace simulated email logging with a provider, retry policy and delivery idempotency only when email delivery becomes part of the product.

### P2 — Original README stretch goals

- [ ] OpenTelemetry traces across the gateway and services.
- [ ] Explicit dead-letter handling, longer redelivery policy and circuit breakers.
- [ ] More integration tests for service restarts, broker outages and concurrent failures.
- [ ] Testcontainers if isolated automated infrastructure tests become worthwhile.

## Definition of done

- [ ] A fresh `docker compose up -d --build` starts healthy services and applies the checked-in migrations.
- [ ] A customer can browse products, change the bag and create an order; the backend calculates the amount.
- [ ] Inventory is reserved once, committed once on successful payment, and released once on failure/expiry.
- [x] Stripe Test Mode opens a hosted Checkout Session and a signed webhook confirms the order.
- [x] Duplicate Checkout requests reuse a Session and duplicate signed Stripe events do not repeat payment/stock transitions in the verified local run.
- [ ] Failure, insufficient stock and session expiry leave the order cancelled and the basket recoverable.
- [ ] Build, unit tests, frontend checks, Docker smoke test and the GitHub Actions run for the final commit pass.
- [ ] Demo notes state the remaining scope limits and no real email, shipment or production payment is claimed.
