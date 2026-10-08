// Read-only checks against the seeded Compose databases and public Catalog API.
// Does not disable Gateway limits, authenticate a user, create orders or change stock.
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const repo = fileURLToPath(new URL("../", import.meta.url));
const base = process.env.API_URL || "http://localhost:8080";
const expected = Number(process.env.EXPECTED_PRODUCTS || 1000000);
assert.ok(Number.isInteger(expected) && expected >= 7 && expected <= 10000000);
const sql = `
SELECT json_build_object('database', current_database(), 'rows', count(*),
 'fixtureRows', count(*) FILTER (WHERE "Id"::text LIKE '10000000-%'),
 'invalidRows', count(*) FILTER (WHERE "Price" <= 0 OR "ImageUrl" NOT IN
 ('/products/laptop.svg','/products/headphones.svg','/products/mouse.svg','/products/keyboard.svg','/products/monitor.svg','/products/ssd.svg')),
 'originalHash', md5(string_agg(row_to_json(p)::text, '' ORDER BY "Id") FILTER (WHERE "Id"::text LIKE '00000000-%')),
 'tableBytes', pg_total_relation_size('"Products"'), 'databaseBytes', pg_database_size(current_database())) FROM "Products" p;
SELECT json_build_object('category', "Category", 'rows', count(*), 'brands', count(DISTINCT "Brand"),
 'minPrice', min("Price"), 'maxPrice', max("Price")) FROM "Products" GROUP BY "Category" ORDER BY "Category";
SELECT json_build_object('missingCatalogIds', count(*)) FROM generate_series(7, ${expected}) n
 WHERE NOT EXISTS (SELECT 1 FROM "Products" WHERE "Id" = ('10000000-0000-0000-0000-' || lpad(n::text,12,'0'))::uuid);
\\connect inventory_db
SELECT json_build_object('database', current_database(), 'rows', count(*),
 'fixtureRows', count(*) FILTER (WHERE "ProductId"::text LIKE '10000000-%'),
 'invalidRows', count(*) FILTER (WHERE "AvailableQuantity" < 0 OR "ReservedQuantity" < 0),
 'outOfStock', count(*) FILTER (WHERE "AvailableQuantity" = 0),
 'lowStock', count(*) FILTER (WHERE "AvailableQuantity" BETWEEN 1 AND 4),
 'originalHash', md5(string_agg(row_to_json(s)::text, '' ORDER BY "ProductId") FILTER (WHERE "ProductId"::text LIKE '00000000-%')),
 'tableBytes', pg_total_relation_size('"Stocks"'), 'databaseBytes', pg_database_size(current_database())) FROM "Stocks" s;
SELECT json_build_object('missingStockIds', count(*)) FROM generate_series(7, ${expected}) n
 WHERE NOT EXISTS (SELECT 1 FROM "Stocks" WHERE "ProductId" = ('10000000-0000-0000-0000-' || lpad(n::text,12,'0'))::uuid);
`;
const output = execFileSync("docker", ["compose", "exec", "-T", "postgres", "sh", "-c",
  'exec psql -X -qAt -U "$POSTGRES_USER" -d catalog_db -v ON_ERROR_STOP=1'],
  { cwd: repo, input: sql, encoding: "utf8", maxBuffer: 1024 * 1024 });
const db = output.split(/\r?\n/).filter((line) => line.startsWith("{")).map((line) => JSON.parse(line));
for (const name of ["catalog_db", "inventory_db"]) {
  const stats = db.find((row) => row.database === name);
  assert.equal(stats.rows, expected, `${name}: expected exact row count`);
  assert.equal(stats.fixtureRows, expected - 6, `${name}: expected reserved fixture IDs`);
  assert.equal(stats.invalidRows, 0, `${name}: invalid data`);
}
assert.equal(db.find((row) => "missingCatalogIds" in row).missingCatalogIds, 0);
assert.equal(db.find((row) => "missingStockIds" in row).missingStockIds, 0);

const timings = [];
async function request(path, status = 200) {
  const start = performance.now();
  const response = await fetch(base + path, { signal: AbortSignal.timeout(30000) });
  const body = await response.text();
  timings.push({ path, status: response.status, milliseconds: Math.round(performance.now() - start), bytes: Buffer.byteLength(body) });
  assert.equal(response.status, status, `${path}: ${body.slice(0, 200)}`);
  return JSON.parse(body);
}
const first = await request("/api/products");
const availableCount = expected - db.find(row => row.database === "inventory_db").outOfStock;
assert.equal(first.totalCount, availableCount);
assert.ok(first.items.every(p => p.availableQuantity > 0));
assert.equal(first.items.length, 24);
assert.equal(first.pageSize, 24);
assert.equal(first.totalPages, Math.ceil(availableCount / 24));
assert.ok(timings.at(-1).bytes < 50000, "Default response must remain bounded");
const second = await request("/api/products?page=2");
assert.equal(second.items.length, 24);
assert.ok(second.items.every((p) => !first.items.some((previous) => p.id === previous.id)));
const availability = await request(`/api/inventory/availability?ids=${first.items.map(p => p.id).join(',')}`);
assert.equal(availability.length, first.items.length);
assert.ok(availability.every(s => s.availableQuantity > 0));
await request('/api/inventory/availability?ids=not-a-guid', 400);
await request(`/api/inventory/availability?ids=${Array(101).fill(first.items[0].id).join(',')}`, 400);
assert.equal((await fetch(base + '/internal/availability')).status, 404, 'Internal snapshots must not be exposed by Gateway');
const all = await request("/api/products?includeOutOfStock=true");
assert.equal(all.totalCount, expected);
const category = await request("/api/products?category=Laptops&sort=price-low&pageSize=100&includeOutOfStock=true");
assert.equal(category.items.length, 100);
assert.equal(category.totalCount, db.find((row) => row.category === "Laptops").rows);
assert.ok(category.items.every((p) => p.category === "Laptops"));
assert.ok(category.items.every((p, i) => i === 0 || category.items[i - 1].price <= p.price));
const expensive = await request("/api/products?sort=price-high&pageSize=12");
assert.ok(expensive.items.every((p, i) => i === 0 || expensive.items[i - 1].price >= p.price));
const original = await request("/api/products?q=MX%20Master%203S");
assert.equal(original.totalCount, 1);
assert.equal(original.items[0].id, "00000000-0000-0000-0000-000000000003");
const generated = await request("/api/products/10000000-0000-0000-0000-000000000007");
assert.ok(generated.description.includes("Synthetic sample SKU 7"));
const empty = await request("/api/products?q=NONEXISTENT_FIXTURE_141");
assert.equal(empty.totalCount, 0);
assert.deepEqual(empty.items, []);
for (const literal of ["%", "_", "\\"]) {
  const result = await request(`/api/products?q=${encodeURIComponent(literal)}`);
  assert.equal(result.totalCount, 0, "Search metacharacters must be treated as literal text");
}
for (const query of ["page=0", "pageSize=101", "pageSize=0", "sort=invalid", `q=${"x".repeat(101)}`])
  await request(`/api/products?${query}`, 400);
const categories = await request("/api/categories");
assert.deepEqual(categories, ["Accessories", "Audio", "Laptops", "Monitors", "Storage"]);

const report = { checkedAt: new Date().toISOString(), expectedProducts: expected,
  databases: db, requests: timings,
  note: "Sequential correctness checks, not a capacity benchmark. Availability projection and name/price indexes now support catalog reads." };
mkdirSync(new URL("../artifacts/", import.meta.url), { recursive: true });
writeFileSync(new URL("../artifacts/catalog-dataset-report.json", import.meta.url), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
console.log("PASS: dataset completeness, stock, bounded pagination, filtering, sorting, search and validation.");
