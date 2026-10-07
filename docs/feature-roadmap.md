# ShopSphere feature completion plan

**Last reviewed:** 2026-10-07
**Source of truth:** `README.md` and `docs/project-brief.md`
**Target:** complete and demonstrate the README's end-to-end checkout. Authentication, fulfillment, shipping, tax, admin, reviews, promotions and email delivery are outside this target.

## Current status

The codebase and Docker stack already implement the core checkout in **Demo** mode. Local Docker, browser, domain-test and signed-webhook-fixture checks passed. That confirms the application flow and webhook handler; it does not confirm the external Stripe API or Stripe's delivery of real events.

- [x] Catalog: six seeded products, categories and product details.
- [x] Basket: Redis storage, quantity changes and product information fetched from Catalog.
- [x] Order creation: server-side prices, validation and repeat-safe checkout ID.
- [x] Inventory: atomic reservation, overselling protection, commit on success and release on failure.
- [x] Messaging: RabbitMQ/MassTransit, transactional outbox/inbox and retry.
- [x] Payment service: hosted Checkout Session integration code, signed webhook validation, persistent event receipts and Demo simulation.
- [x] Customer flow: catalog, product detail, bag, checkout and order-status pages.
- [x] Notification worker: confirmation/cancellation events appear in logs as simulated emails.
- [x] Initial EF migrations, Docker Compose, CI workflow, smoke checks and synthetic signed-webhook checks.
- [ ] External Stripe Test Mode checkout has **not** been exercised with Stripe credentials and a running Stripe CLI listener.

The next release gate is a real Stripe **test-mode** checkout from the app, confirmed by its signed webhook. The browser return URL alone is not proof of payment.

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
- [ ] Arrange access to Stripe test-mode credentials. Keep keys in the developer's ignored `.env`; do not put them in source control.
- [ ] Install or update the official Stripe CLI and confirm its listener can reach the local webhook endpoint.
- [ ] Agree on one isolated test customer and a repeatable run log.

**Day 1 exit criteria:** every teammate can run the Demo journey; the Stripe test key and webhook listener are ready for Day 2.

### Day 2 — Prove the distributed payment flow

**Developer 3 — Stripe**
- [ ] Set `PAYMENT_MODE=Stripe`, `STRIPE_SECRET_KEY` and the listener's `STRIPE_WEBHOOK_SECRET` locally; recreate the Payment container.
- [ ] Create an order in the storefront, continue to hosted Checkout, pay with Stripe test details, and confirm that the signed `checkout.session.completed` event confirms the order.
- [ ] Check PostgreSQL/order status, RabbitMQ, Seq and the UI. Confirm inventory is committed exactly once and the purchased quantity is removed from the basket.
- [ ] Verify that an invalid signature does not change payment or order state. Confirm a repeated event is acknowledged without a second business transition.
- [ ] Check a failed or expired session. Confirm the order is cancelled, reserved inventory is released once, and the basket remains available.

**Developer 2 — Order processing**
- [x] The synthetic webhook check covers invalid signatures, an incorrect amount, repeated success/expiry events and expiry compensation.
- [ ] Review the live Stripe run against the synthetic checks; fix any mismatch in session references, currency, amounts or terminal-state behavior.
- [ ] Confirm consumers recover after RabbitMQ is briefly unavailable, with failed messages visible in the broker.

**Developer 1 — Storefront**
- [ ] Verify the hosted Checkout redirect, return-to-order page and polling until a webhook changes the order status.
- [ ] Verify Stripe mode never exposes a secret key or shows Demo payment controls.
- [ ] Capture any usability issues from the presenter and prioritize only blockers for the buying journey.

**Day 2 exit criteria:** one test-mode Stripe payment confirms an order through the signed webhook; a failure or expiry releases stock; no payment path trusts the browser redirect.

### Day 3 — Release rehearsal

**Developer 1 — Demo experience**
- [ ] Start from the documented setup and rehearse the demo in order: product → bag → checkout → payment → order confirmed.
- [ ] Rehearse the failure path and explain the inventory release and retained basket.
- [ ] Keep the UI limited to the README scope. Do not add admin, promotions or other stretch features during this day.

**Developer 2 — Reliability and CI**
- [ ] Run `dotnet build ShopSphere.sln -c Release`, `dotnet test ShopSphere.sln -c Release`, frontend lint/typecheck/build and `docker compose config --quiet`.
- [ ] Run `node scripts/smoke-test.mjs` on a fresh stack; confirm it exits successfully without relying on previous test data.
- [ ] Verify the GitHub Actions run for the release commit is green. The current workflow builds/tests .NET, validates Compose, and lints/builds the frontend; the full Docker smoke test remains a local check.
- [ ] Review RabbitMQ error queues and Seq after the rehearsal. Write down any known limitation rather than claiming it is covered.

**Developer 3 — Payment and project handoff**
- [ ] Rehearse Stripe from the storefront using test mode and keep the webhook listener running for the presentation.
- [ ] Ensure the Stripe keys and `.env` are ignored by Git. Document how to switch back to Demo mode.
- [ ] Update README instructions only if the rehearsal finds a mismatch. Keep the project brief as the reference for excluded scope.

**Day 3 exit criteria:** a clean-stack demo, a successful Stripe test payment, a demonstrated compensation path, passing CI, and a short runbook the team can follow.

## Follow-up after the core demo

These items are deliberately outside the current definition of done. Prioritize them only when the project will be used beyond a local, unauthenticated demonstration.

### P1 — Before a shared or public deployment

- [ ] Add authentication and authorize every basket, payment and order lookup against the signed-in customer. Today, the browser-generated customer ID and order URL are identifiers, not access control.
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
- [ ] Stripe Test Mode opens a hosted Checkout Session and a signed webhook—not the redirect—confirms the order.
- [ ] Duplicate checkout requests and duplicate Stripe events do not create a second order, charge transition or stock adjustment.
- [ ] Failure, insufficient stock and session expiry leave the order cancelled and the basket recoverable.
- [ ] Build, unit tests, frontend checks, Docker smoke test and the GitHub Actions run for the final commit pass.
- [ ] Demo notes state the remaining scope limits and no real email, shipment or production payment is claimed.
