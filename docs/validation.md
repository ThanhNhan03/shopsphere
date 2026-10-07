# Validation

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
