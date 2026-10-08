# Validation

## Catalog availability and Add to bag — 2026-10-08

- Scanned the Catalog/card/Basket/Inventory query and reservation paths with codebase-memory plus direct source checks for metadata/partial parsing. Found that catalog cards lacked any stock check, while around 5% of the fixture intentionally has zero stock; shared Basket add/update error wording also gave the wrong action on Add.
- Product lists now filter a versioned Catalog availability projection before count/pagination by default; Include out of stock is explicit. The projection bootstrapped all 1,000,000 Inventory rows through bounded private HTTP pages and receives transactional availability events. A single live batch per visible page gates card purchase actions, including existing-bag capacity. Add errors no longer instruct shoppers to remove an item they could not add.
- Current read-only verification: 1,000,000 products/stock rows, **950,057 available** and **49,943 sold out**; default/all counts, stock-positive pages, stable pagination, search/category/sort and request validation passed through the real Gateway. Batch bounds/invalid IDs and the private snapshot's absence from Gateway also passed. Name/price/projection indexes were added; measured sequential timings are not load-capacity claims.
- Inventory/Catalog/Basket/frontend Docker builds, TypeScript, frontend ESLint and the full **41-test .NET suite** passed. Schema migrations do not reset stock. All rebuilt services are healthy.
- Real Compose checks passed for reserve-to-zero removing a generated product from default search, include-out-of-stock retaining its detail, and Demo compensation restoring catalog eligibility and stock. One isolated cancelled QA order remains as evidence; test keys were removed. User bags and original quantities were not changed.
- Real RabbitMQ/Catalog consumer checks passed for a newer version, delayed older version and repeated version. The owned fixture projection row was removed; its inbox records remain processing evidence.
- In-process actual React catalog/provider checks passed for default/toggle states, initial live-stock loading, disabled sold-out Add, a bag already holding the maximum and one batched stock request per page. Existing quantity focus/keyboard/rollback/cache checks still passed. This does not claim a new browser visual/mobile audit.
- Policy/architecture/limitations and reproduction: `docs/catalog-availability.md`. The projected list can lag stock changes; live card reads and the final Inventory reservation remain necessary. Ten-minute reconciliation also covers future insert-only fixture seeding.

## Quantity focus and per-item pricing — 2026-10-07

- Quantity plus/minus controls retain focus and hover styling during requests. `aria-disabled` and guarded handlers prevent further writes while saving or at a quantity limit; the input is read-only while saving. Temporary request locks no longer dim the buttons. Persistent min/max/stock limits remain visually distinct.
- Bag rows now show a dedicated Unit price before Quantity and Item total, removing the duplicate price beneath the name. The responsive grid puts unit price above the quantity/line-total pair on narrow screens. Item total uses the server-confirmed unit price and quantity.
- Production frontend build/TypeScript, ESLint and existing in-process React checks passed, including retained button focus, blocked repeated pending/limit clicks, stock bounds, rollback and direct-entry keyboard behavior. Basket cache assertions also passed. Deployed the healthy frontend container; visual/mobile screenshots were not verified.

## Stable bag action labels — 2026-10-07

- `Remove` and `Continue to checkout` were inheriting the global 48% disabled opacity on every quantity request. Scoped pending action styles now preserve their opacity while retaining native disabled behavior; checkout remains visibly disabled for insufficient stock or a basket error.
- Frontend production build/TypeScript, ESLint and diff whitespace checks passed. Rebuilt and deployed the healthy frontend container. This small style correction was reviewed in source; no new browser visual audit was performed.

## Basket updates and stock checks — 2026-10-07

- Diagnosed the reported cancellation against persisted order/stock data: the order requested three original Keychron K2 keyboards while only two were available, with no reserved units. The previous basket allowed 1–99 without checking stock; Inventory correctly rejected the later reservation. Existing orders, user baskets and original stock were not reset.
- Kept the visible `Quantity` caption and input styling stable during writes. Successful POST/PUT mutations now use the returned basket directly; DELETE updates the cached lines/totals. Background basket reads receive an AbortSignal and in-flight reads are cancelled before mutations. Writes share a mutation scope; conflicts refresh stock. Existing five-second polling remains.
- Basket responses include live `availableQuantity` and `canCheckout`. Product/bag controls cap increases at current stock, disclose low/out-of-stock lines, allow oversized saved baskets to be reduced and prevent checkout while unavailable or updating. Redis Lua checks cumulative/concurrent adds atomically against the fetched stock snapshot.
- Ordering checks the refreshed basket before persisting/publishing an order and returns HTTP 409 for a known shortage. Inventory lookup failures fail closed with HTTP 503; missing stock returns zero. These checks do not reserve stock: the existing Inventory transaction/row locks still arbitrate competing orders, and another buyer can consume stock between preflight and reservation.
- Basket/Ordering/frontend production Docker builds including TypeScript, frontend ESLint, Compose configuration and healthy deployment passed. The complete .NET suite passed **38 tests**, including ten Inventory HTTP adapter cases.
- In-process React/jsdom assertions passed for the fixed pending caption, stock caps, reducing stale quantities, keyboard/focus behavior and mutation response cache updates without an extra GET. DELETE totals and conflict-only refresh also passed. These are component/hook checks; visual/mobile bag screenshots were not verified in this change.
- `node scripts/basket-stock-test.mjs` passed on the real Compose services: cumulative/concurrent adds, sold-out rejection, legacy oversized bag, HTTP 409 with no persisted order, valid reservation and Demo failure compensation. It used isolated basket keys and generated products, persisted one isolated cancelled QA order, removed its basket keys and verified that reservation release naturally restored the original test stock. No payment was charged. This is internal service integration coverage, not an authenticated Gateway/browser walkthrough.
- Used the installed codebase-memory server to index/search/trace Basket and Ordering paths. Partial TSX parsing and changed-metadata coverage were resolved through direct source reads; graph coverage is not treated as complete.

## Bag quantity control — 2026-10-07

- Replaced the 99-option native select with a styled minus/input/plus control, shared SVG icons and Manrope typography. Buttons have 44px targets; input uses a numeric keyboard, supports direct entry and remains bounded to 1–99. Captions separate quantity and line total; narrow-screen CSS moves the controls below the product information.
- Enter or leaving the control saves a draft; Escape cancels it. Focus moving from input to a quantity button does not save before the button action. Pending values are displayed while updating, controls are disabled and the current row announces its busy state. Failed mutations return to the server quantity and retain existing basket error feedback.
- Production frontend build/TypeScript and ESLint passed. In-process React/jsdom checks passed for edited quantity plus increment producing exactly one update, pending/disabled states, rollback after failure, Enter/blur commits, Escape cancellation and 1–99 bounds. These checks use synthetic component props and do not access or alter the user's real bag.
- Browser access to the isolated preview at localhost:3011 was denied. No workaround was attempted; visual/mobile screenshots remain unverified for this change. The temporary preview services were removed. Responsive/focus/reduced-motion rules were reviewed in source; this is not a visual accessibility audit.
- The final frontend image was rebuilt and deployed to the normal Compose store. Existing backend auth, rate limits and basket API contracts remain intact.

## Million-product sample dataset — 2026-10-07

- Generated 999,994 deterministic synthetic products and matching stock records, retaining the original six. Catalog and Inventory now each contain exactly 1,000,000 rows; completeness checks found zero missing generated IDs and zero invalid prices/icons/stock quantities.
- Original product/stock checksums match the pre-load snapshot, including previously consumed inventory. Re-running the first 1,000 generated IDs left both totals at 1,000,000, without duplicate rows. Seeder uses bounded transactions and insert-only conflict handling; separate database commits are recoverable by rerunning after interruption.
- Full 1,000,000-target run took 94.2 seconds on the current local Docker environment (after a 1,000-row trial; other checks were running concurrently). Catalog table including indexes: 296,640,512 bytes (282.9 MiB); stock table including indexes: 83,787,776 bytes (79.9 MiB). Database totals are larger and WAL/other volumes are additional storage.
- Product listing now returns a bounded page object, default 24 and maximum 100 items. Search/category/sort/pagination run in PostgreSQL, with stable ID tie-breakers and validation. No Catalog read indexes/cache have been added; literal substring search and OFFSET provide a baseline for later optimization.
- Read-only verification passed through the real Gateway: bounded payloads, nonoverlapping pages, ascending/descending prices, category counts, generated product details, original product search, empty/literal-wildcard searches and invalid query limits. Default page payload was 7,664 bytes; sequential search samples took 628–923 ms. These are sanity observations, not concurrent throughput or p95 measurements.
- Browser verified 24 cards, loaded shared SVGs, 1,000,000 total and 41,667 pages, successful page change, and Laptops resetting to page 1 with 299,425 results. No orders/payments were created during this validation.
- Catalog/frontend Docker builds including TypeScript passed; frontend ESLint passed; existing .NET suite: 28 passed. Compose configuration validation and all running service health states passed.
- Reproduction: `scripts/seed-catalog.ps1`, `scripts/verify-catalog-data.mjs`; contract/distribution/limitations: `docs/catalog-dataset.md`. Local detailed report: ignored `artifacts/catalog-dataset-report.json`.

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

External Stripe API creation and a real Stripe test payment require user-supplied test credentials and an active webhook listener. Signed fixtures exercise our handler and persistence, not Stripe's external infrastructure.

Dependency note: npm audit --omit=dev reported no production vulnerabilities. The development-only Next.js ESLint chain includes braces <=3.0.3, GHSA-vfj7-8cjw-p6xm. The registry currently has no patched braces release; do not force npm's suggested downgrade to an incompatible Next.js lint configuration. Recheck on future dependency upgrades.

## Admin restoration and million-product compatibility — October 8, 2026

- The running Docker containers predated the local-account/admin feature: `/admin` and `/register` returned 404, the Gateway lacked account/admin environment configuration, and `identity_db` was missing. The corresponding authentication/admin source files were retained by the merge.
- Ran the existing PostgreSQL initializer, preserved configured bootstrap credentials, and rebuilt/recreated Gateway, frontend, Catalog, Inventory, Basket, and Ordering. Started the pinned-source MinIO service without removing existing data volumes.
- Fixed administration compatibility with the million-product dataset: product search/visibility/pagination runs on the server (default 50, maximum 100), stock reads require at most 100 explicit product IDs, and the overview uses aggregate counts plus at most ten projected low-stock products. The UI no longer loads the complete product/stock lists or performs repeated full-list stock lookups.
- Backend Release build: 0 warnings, 0 errors. All 41 xUnit cases passed, including admin query bounds, stock batch validation, and combined migration-model checks. Frontend lint/typecheck and Docker production build passed; Compose configuration validated.
- Real HTTP checks passed for local administrator login/session/logout through both Gateway and the storefront proxy, all four admin routes and registration, bounded product/stock responses, and an overview covering 1,000,000 products. The browser displayed the email/password login form and registration link.
- The authenticated admin smoke harness passed: customer/admin separation, Origin protection, private MinIO upload/read/replacement, product visibility and concurrent edit protection, stock adjustment history/concurrency, hidden-product checkout denial, server pricing, idempotent checkout, Demo payment confirmation, and order reporting. One isolated customer and confirmed demo order remain as test evidence; the test product is hidden and its remaining available stock was cleared.
