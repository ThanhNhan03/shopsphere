// Real Compose service checks with isolated basket keys. No Google session or user's bag is used.
// Requires Demo mode for the reservation/compensation check; stock is never reset by this script.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';

function sql(statement) {
  return execFileSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c',
    'exec psql -X -qAt -U "$POSTGRES_USER" -d inventory_db -v ON_ERROR_STOP=1 -c "$1"', 'sh', statement], { encoding: 'utf8' }).trim();
}
const productId = sql(`SELECT "ProductId" FROM "Stocks" WHERE "AvailableQuantity" = 2 AND "ReservedQuantity" = 0 AND "ProductId"::text LIKE '10000000-%' LIMIT 1`);
const emptyId = sql(`SELECT "ProductId" FROM "Stocks" WHERE "AvailableQuantity" = 0 AND "ReservedQuantity" = 0 AND "ProductId"::text LIKE '10000000-%' LIMIT 1`);
assert.match(productId, /^[a-f0-9-]{36}$/);
assert.match(emptyId, /^[a-f0-9-]{36}$/);
const mode = execFileSync('docker', ['compose', 'exec', '-T', 'payment-api', 'printenv', 'Payment__Mode'], { encoding: 'utf8' }).trim();
assert.equal(mode, 'Demo', 'Run the complete flow only with local Demo payments');
const customers = Array.from({ length: 4 }, () => `stock-check-${randomUUID()}`);
execFileSync('docker', ['compose', 'exec', '-T', 'redis', 'redis-cli', 'HSET', `basket:${customers[3]}`, productId, '3']);

async function run({ productId, emptyId, customers }) {
  const { default: assert } = await import('node:assert/strict');
  const { randomUUID } = await import('node:crypto');
  const basket = 'http://basket-api:8080';
  const ordering = 'http://ordering-api:8080';
  const inventory = 'http://inventory-api:8080';
  const payment = 'http://payment-api:8080';
  const catalog = 'http://catalog-api:8080';
  async function call(base, path, method = 'GET', body) {
    const response = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(15000) });
    return { status: response.status, data: await response.json().catch(() => null) };
  }
  async function ok(base, path, method = 'GET', body) {
    const result = await call(base, path, method, body);
    assert.ok(result.status >= 200 && result.status < 300, `${method} ${path}: ${JSON.stringify(result)}`);
    return result.data;
  }
  async function until(read, predicate, label) {
    for (let i = 0; i < 100; i++) {
      const value = await read();
      if (predicate(value)) return value;
      await new Promise((resolve) => setTimeout(resolve, 150));
    }
    throw new Error(`Timed out: ${label}`);
  }
  const item = `/items/${productId}`;
  const path = (n) => `/api/basket/${customers[n]}`;
  const add = { productId, quantity: 1 };
  let created;
  try {
    const initial = await ok(inventory, `/api/inventory/${productId}`);
    assert.equal(initial.availableQuantity, 2);
    let bag = await ok(basket, path(0) + '/items', 'POST', { ...add, quantity: 2 });
    assert.equal(bag.items[0].availableQuantity, 2);
    assert.equal(bag.canCheckout, true);
    assert.equal((await call(basket, path(0) + item, 'PUT', { quantity: 3 })).status, 409);
    assert.equal((await call(basket, path(0) + '/items', 'POST', add)).status, 409);
    assert.equal((await ok(basket, path(0))).items[0].quantity, 2);

    assert.equal((await call(basket, path(1) + '/items', 'POST', { productId: emptyId, quantity: 1 })).status, 409);
    assert.equal((await ok(basket, path(1))).items.length, 0);
    await ok(basket, path(2) + '/items', 'POST', add);
    const concurrent = await Promise.all([call(basket, path(2) + '/items', 'POST', add), call(basket, path(2) + '/items', 'POST', add)]);
    assert.deepEqual(concurrent.map((r) => r.status).sort(), [200, 409]);
    assert.equal((await ok(basket, path(2))).items[0].quantity, 2);

    bag = await ok(basket, path(3));
    assert.equal(bag.items[0].quantity, 3);
    assert.equal(bag.canCheckout, false);
    const rejectedId = randomUUID();
    const rejected = await call(ordering, '/api/orders', 'POST', { checkoutId: rejectedId,
      customerId: customers[3], customerName: 'Stock QA', email: 'stock-qa@example.test' });
    assert.equal(rejected.status, 409);
    assert.match(rejected.data.detail, /only 2 available/i);
    assert.equal((await call(ordering, `/api/orders/${rejectedId}`)).status, 404, 'Known shortage must not persist an order');
    bag = await ok(basket, path(3) + item, 'PUT', { quantity: 2 });
    assert.equal(bag.canCheckout, true);

    created = await ok(ordering, '/api/orders', 'POST', { checkoutId: randomUUID(),
      customerId: customers[0], customerName: 'Stock QA', email: 'stock-qa@example.test' });
    await until(() => ok(ordering, `/api/orders/${created.id}`), (o) => o.status === 'AwaitingPayment', 'valid reservation');
    await until(() => call(payment, `/api/payments/${created.id}`), (r) => r.status === 200, 'Demo payment');
    const reserved = await ok(inventory, `/api/inventory/${productId}`);
    assert.equal(reserved.availableQuantity, 0);
    assert.equal(reserved.reservedQuantity, 2);
    const product = await ok(catalog, `/api/products/${productId}`);
    const search = `/api/products?q=${encodeURIComponent(product.name)}`;
    await until(() => ok(catalog, search), (p) => p.totalCount === 0 && p.items.length === 0, 'reserved product hidden from available catalog');
    const unavailable = await ok(catalog, search + '&includeOutOfStock=true');
    assert.equal(unavailable.totalCount, 1);
    assert.equal(unavailable.items[0].availableQuantity, 0);
    await ok(payment, `/api/payments/${created.id}/simulate`, 'POST', { paid: false });
    await until(() => ok(inventory, `/api/inventory/${productId}`), (s) => s.availableQuantity === 2 && s.reservedQuantity === 0, 'reservation release');
    await until(() => ok(catalog, search), (p) => p.totalCount === 1 && p.items[0]?.availableQuantity === 2, 'released product restored to catalog');
    console.log('PASS: availability events hide reserved-out stock before pagination and restore it after compensation.');
    console.log('PASS: stock caps, cumulative/concurrent adds, sold-out rejection, stale-basket 409 without creating an order, valid reservation and Demo compensation.');
  } finally {
    if (created) {
      const current = await call(payment, `/api/payments/${created.id}`);
      if (current.status === 200 && current.data.mode === 'Demo')
        await ok(payment, `/api/payments/${created.id}/simulate`, 'POST', { paid: false });
    }
    for (const customer of customers) await ok(basket, `/api/basket/${customer}`, 'DELETE');
  }
}

try {
  const result = execFileSync('docker', ['compose', 'exec', '-T', 'frontend', 'node', '--input-type=module'],
    { input: `await (${run.toString()})(${JSON.stringify({ productId, emptyId, customers })});`, encoding: 'utf8', timeout: 90000 });
  console.log(result.trim());
  const final = JSON.parse(sql(`SELECT json_build_object('available', "AvailableQuantity", 'reserved', "ReservedQuantity") FROM "Stocks" WHERE "ProductId" = '${productId}'`));
  assert.deepEqual(final, { available: 2, reserved: 0 }, 'No stock reset; compensation must restore the initial state');
  console.log('PASS: test stock restored naturally; user baskets and original stock were not modified.');
} finally {
  for (const customer of customers)
    execFileSync('docker', ['compose', 'exec', '-T', 'redis', 'redis-cli', 'DEL', `basket:${customer}`]);
}
