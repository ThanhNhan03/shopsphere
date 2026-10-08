# Validation

## Stock-aware catalog and checkout — 2026-10-08

- Catalog now lists available products by default, supports category/search/sort/pagination, and exposes an explicit out-of-stock filter. Live stock batches drive storefront availability and quantity limits.
- Inventory stock changes publish versioned events; Catalog applies newer versions and reconciles bounded snapshots. Basket add/update and Ordering checkout reject known shortages; the final locked Inventory reservation remains authoritative for competing purchases.
- `dotnet test ShopSphere.sln -c Release`: **40 passed**. Frontend ESLint, `next typegen` plus TypeScript, and the Next.js production build passed. `docker compose config --quiet` passed.
- The real RabbitMQ projection verification script is available at `scripts/verify-availability-projection.mjs`; it was not run in this validation. Docker images for Catalog, Basket, Ordering, Inventory and the frontend built successfully; Compose services were not restarted and no data volume was changed.

## Real Stripe test-mode payments — 2026-10-08

- Configured the supplied test key in ignored `.env`; the CLI signing secret is captured locally without printing credentials. The official Stripe CLI 1.53.1 Windows binary was verified against its release SHA-256 before use. The background listener forwards supported Checkout events to Gateway on port 8180.
- `stripe-checkout-test.mjs start` passed against real Stripe: concurrent Checkout requests reuse one Session; authoritative amount/currency and order metadata match; return URLs are correct; Demo simulation is disabled; anonymous and other-customer access is rejected; invalid signatures return 400.
- Explicitly expired a real Stripe Session through Stripe's API. The CLI-delivered signed expiry event cancelled the order, returned reserved stock and retained the basket.
- Browser verified storefront sign-in, the order page's Stripe resume button, hosted sandbox Checkout and the return page showing **Confirmed**, payment received and an empty bag. Stripe's API independently reported `livemode=false`, `payment_status=paid` and USD 1.23. Adaptive Pricing recorded a separate VND 33,223 presentment amount; the USD webhook matching succeeded.
- `stripe-checkout-test.mjs verify` passed: the actual signed completion webhook confirmed the order, committed reserved inventory, removed the purchased quantity and exposed the confirmed admin order. Replaying the actual completion event twice with a fresh local signature left payment and inventory unchanged. This verifies our durable receipt handling, not Stripe Dashboard resend delivery.
- The authenticated synthetic fixture harness also passed invalid signatures, wrong amounts, success/expiry duplicate receipts and compensation. Its temporary Payment container was removed. It consumed one seeded MX Master 3S; other tests used isolated products. Isolated test products were hidden and their remaining available stock cleared, including the earlier interrupted check; customer/order and audit history remain.
- Backend Release build: zero warnings/errors. xUnit: **33 passed**. Frontend ESLint and Docker production build including TypeScript: passed. Payment/frontend images were rebuilt and deployed; Compose configuration passed and all services with health checks are healthy. Seq is running without a configured health check.
- Screenshot: ignored `artifacts/stripe-order-confirmed.jpg`. Runbook: [Stripe setup and verification](stripe.md).
- Broker-outage/restart recovery, a new-machine/full clean-stack rehearsal and GitHub Actions for these uncommitted changes were not tested. Live payments, refunds, shipment and actual email delivery remain outside this change.

## Shared device icons / photo revert — 2026-10-07

- Reverted the vendor-photo experiment and removed its runtime assets, mapping and download script. Restored the six device SVGs, with neutral SSD labeling for reuse across brands.
- Added a shared device-icon registry, category fallbacks and a generic unknown-device placeholder. Existing catalog and saved-basket SVG paths remain supported; no product data or quantities were changed.
- Browser verified all eight home images (six products plus banners) load from the restored local SVGs. The Manrope slogan consistency correction remains intact.
- Frontend production build including TypeScript and ESLint passed. Compose frontend was rebuilt and deployed; the initial Docker snapshot cache error was resolved by a fresh build. Production dependency audit returned zero vulnerabilities.
- Screenshot: ignored `artifacts/ui-device-icons-restored.png`.

## Typography consistency — 2026-10-07

- Removed the Georgia/italic slogan overrides from home, audio and sign-in banners. All three use Manrope 700 with normal style; accent spans inherit the heading's size, line height, weight and letter spacing.
- Browser computed styles confirmed matching font family, size, weight, style and letter spacing between each slogan heading and its highlighted word.
- Visually checked home at 320, 375 and 1440px and sign-in at 375/1440px. No horizontal page or slogan overflow was observed.
- Production build (including TypeScript) and ESLint passed; Compose frontend was rebuilt and deployed. Screenshot: ignored `artifacts/ui-typography-desktop.png`.

## UI/UX redesign — 2026-10-07

- Applied the local UI/UX Pro Max skill; the design direction, tokens and covered flows are recorded in `ui-design.md`.
- Next.js production build (including TypeScript) and ESLint passed inside the frontend Docker build stage. The frontend was rebuilt and deployed through Compose.
- Browser verified: category filtering, price sorting, brand search, empty search results and clearing search with URL recovery; product details and bounded quantity controls.
- Real Google sign-in through the account chooser succeeded. Adding a Keychron K2 showed success feedback, a saved bag and an authoritative $89 total. Checkout prefilled account details.
- An empty required name displayed an inline error and focused `customer-name`. No order was created by the invalid submission.
- A valid checkout created local demo order `1f57323a-3f52-4b9d-940e-feb89b351dad`. Demo failure changed it to Cancelled; no card was charged. The bag retains one keyboard for another attempt.
- Visually checked home, product, login, empty/full bag, checkout, order/payment and missing-payment-link screens. Home responsive checks covered 320, 375, 768, 1024 and 1440px plus 812px landscape; no horizontal page overflow was observed. Checkout, product and order were also checked at 375px.
- Visible focus, semantic form labels/errors, touch targets and reduced-motion CSS are included. This is browser QA, not a full accessibility audit.
- Local preview evidence is saved in ignored `artifacts/ui-home-desktop.png` and `artifacts/ui-home-full.png`.

## Rate limiting and Google account selection — 2026-10-07

- .NET suite: 28 passed, including quotas, independent user buckets, Retry-After, window recovery, forwarding-header spoof resistance and login URL variants.
- Gateway/frontend Docker builds, frontend TypeScript, ESLint and Compose validation: passed.
- Browser: Google account chooser appears with `prompt=select_account` and offers **Use another account** even with an existing Google session.
- Deployed Gateway burst check returned HTTP 429; health remained 200. Counters are local to each Gateway instance; see the login runbook for proxy and multi-instance limits.

## Google authentication validation — 2026-10-07

- Real Google OAuth sign-in: passed with the local configured client.
- Session survives page reload; signed-in basket read/write and checkout name/email prefill: passed.
- Logout removes private access; signing back in restores the account's bag and requested return page: passed.
- .NET suite: 23 passed, including valid OAuth callback fixtures and authentication/ownership/Origin checks.
- Frontend build/TypeScript and ESLint: passed for the login implementation.
- Fixed the Docker Data Protection volume ownership so the non-root Gateway can create persistent session keys.
- Browser walkthrough left one Keychron K2 in the user's bag, without creating an order or charging a payment.
- Second-account browser isolation remains unverified; cross-account rejection is covered by automated Gateway tests.

The baseline checks below predate mandatory Google sign-in; anonymous smoke scripts need an authenticated harness for protected Gateway endpoints.

Verified locally on 2026-10-07:

- .NET solution build: passed, zero warnings/errors.
- xUnit: 14 passed.
- Next.js production build, ESLint and TypeScript checks: passed.
- All eight application Docker images built; application and infrastructure health checks passed.
- Docker Compose configuration: passed; four databases created and committed migrations applied.
- HTTP smoke test on the real Compose stack: passed, including concurrent checkout retries, authoritative pricing, success/failure, duplicate settlement, basket cleanup, compensation, insufficient inventory and simultaneous stock contention.
- Signed synthetic Stripe webhooks on PostgreSQL/RabbitMQ: passed for invalid signatures, wrong amounts, success, expiry, duplicate receipts and inventory release.
- Browser walkthrough: catalog, add to bag, change quantity, checkout, demo payment, Confirmed order and empty bag passed.
- Notification logs recorded simulated confirmation/cancellation emails.
- NuGet vulnerability check including transitive packages: no vulnerable packages reported.

Local host ports were overridden to frontend 3100 and gateway 8180 because other applications already used 3000/8080. Repository defaults remain 3000/8080.

Tests persisted demo orders and consumed one SSD, one mouse and two keyboards. They did not reset data volumes.

Checks are reproducible with dotnet test, frontend lint/typecheck/build, docker compose config, scripts/smoke-test.mjs and scripts/stripe-webhook-test.mjs as documented in the README.

At the baseline verification above, external Stripe API creation and a real Stripe test payment were unverified. They were subsequently verified with test credentials and an active listener on 2026-10-08, as recorded at the top of this document. Signed fixtures alone exercise our handler and persistence, not Stripe's external infrastructure.

Dependency note: npm audit --omit=dev reported no production vulnerabilities. The development-only Next.js ESLint chain includes braces <=3.0.3, GHSA-vfj7-8cjw-p6xm. The registry currently has no patched braces release; do not force npm's suggested downgrade to an incompatible Next.js lint configuration. Recheck on future dependency upgrades.
