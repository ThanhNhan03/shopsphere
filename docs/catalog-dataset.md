# Million-product sample dataset

This dataset is for local database/query experiments. It contains synthetic product specifications, not an authoritative vendor catalog. One million rows measures data scale; it does not establish concurrent request capacity.

## Generate or resume

Start the normal Compose stack and apply migrations first. Deploy the bounded Catalog API/frontend before loading the large fixture:

```powershell
docker compose build catalog-api frontend
docker compose up -d --no-deps catalog-api frontend
powershell -NoProfile -File scripts/seed-catalog.ps1 -Count 1000000
```

`Count` includes the original six products. With a six-product baseline, the default adds 999,994 products and corresponding stock rows. Existing unrelated rows are never deleted; if you add your own data, total rows may exceed Count.

- Original six UUIDs remain in the `00000000-...` namespace. Generated IDs use `10000000-0000-0000-0000-` plus a 12-digit sequence from 7 through Count.
- IDs and values are deterministic. `ON CONFLICT DO NOTHING` preserves existing products and stock, including already consumed/reserved quantities.
- Transactions contain at most 50,000 rows per database. Catalog and Inventory commit independently; a failure can leave the last batch partially complete across databases. Re-running repairs missing rows before verification.
- No truncation, stock reset, giant EF migration, million-row JSON file or per-product HTTP calls are used. PostgreSQL generates data directly with `generate_series`.
- Re-running a lower Count never shrinks the dataset. Progressively load 10,000, 100,000 and 1,000,000 rows if comparing data sizes; use separate DB snapshots/instances for comparisons that need the smaller dataset again.
- An advisory lock serializes each database's seed batch. Migrations and application business rules still own schema and stock behavior.
- `ANALYZE` runs after loading so PostgreSQL has updated statistics. WAL and normal durability remain enabled.

Approximate device distribution: laptop 30%, headphones 20%, mouse 15%, keyboard 15%, monitor 12%, SSD 8%. Mouse/keyboard share Accessories. Brands/prices/specifications are independently varied using deterministic hashes; around 5% of generated stock is empty, 5% low and the remainder 10–500 units. This is a repeatable starting distribution, not a model of real sales traffic.

All products reuse the six device SVGs. Seed data does not modify existing orders, payments, baskets or accounts.

## Bounded product API

`GET /api/products` now returns a page object instead of an unbounded array:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 24,
  "totalCount": 950057,
  "totalPages": 39586
}
```

Query options: `page` (1–1,000,000), `pageSize` (1–100, default 24), exact `category`, `q` (case-insensitive literal substring in name/brand/category, max 100 characters), `sort` (`name`, `price-low`, `price-high`), `includeOutOfStock` (default false). Sorting includes an ID tie-breaker. Invalid limits/sorts return HTTP 400. Detail and categories endpoints keep their contracts. The example count is the current available subset; `includeOutOfStock=true` includes all 1,000,000 products. Counts change with reservations and sales.

Frontend loads one page, resets pagination when category/search/sort changes and uses Previous/Next controls. The original spotlight product is loaded separately. Historical smoke scripts use original product IDs, independent of pagination; their authenticated harness is still a separate outstanding task.

The subsequent availability fix adds a versioned query projection and name/price/projection indexes; no Catalog response cache is added. OFFSET deep pages and literal substring search may still be slow at this scale; compare execution plans and keyset pagination/search index options next. See [catalog availability](catalog-availability.md). After loading more fixture data, allow the ten-minute reconciliation or restart Catalog for an immediate pass before verification; its initial availability pass temporarily gates listings with HTTP 503.

## Verify

```powershell
node scripts/verify-catalog-data.mjs
```

Set `API_URL` for a different Gateway port; `EXPECTED_PRODUCTS` defaults to 1,000,000. Verification checks exact row counts on the six-product baseline, every generated ID in both databases, valid prices/icons/stock, bounded responses, nonoverlapping pages, sorting, category filters, literal search and rejected request limits. It writes an ignored local report to `artifacts/catalog-dataset-report.json` with table/database sizes and sequential HTTP timings.

The report's HTTP times include Gateway and serialization. They are sanity observations, not p95 under concurrency; do not claim supported users/RPS from this report. Future load tests must separate Catalog internal capacity from public Gateway throttling and keep errors/HTTP 429 visible.

For authoritative guidance: PostgreSQL documents [series generation](https://www.postgresql.org/docs/current/functions-srf.html), [post-load ANALYZE](https://www.postgresql.org/docs/current/populate.html#POPULATE-ANALYZE) and [LIMIT/OFFSET behavior](https://www.postgresql.org/docs/current/queries-limit.html).
