// Requires the Compose stack in Demo mode. Uses isolated customers and the seeded SSD.
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
const base = process.env.API_URL || "http://localhost:8080";
async function request(path, method = "GET", body) {
  const response = await fetch(base + path, { method, headers: { "Content-Type": "application/json" }, body: body && JSON.stringify(body) });
  const result = await response.json().catch(() => undefined);
  assert.ok(response.ok, `${method} ${path}: ${response.status} ${JSON.stringify(result)}`);
  return result;
}
async function until(read, predicate, label) {
  for (let i = 0; i < 60; i++) {
    const result = await read();
    if (predicate(result)) return result;
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`Timed out: ${label}`);
}
const product = await request("/api/products/00000000-0000-0000-0000-000000000006");
const initial = await request(`/api/inventory/${product.id}`);
async function checkout(quantity) {
  const customerId = randomUUID();
  await request(`/api/basket/${customerId}/items`, "POST", { productId: product.id, quantity, unitPrice: 0.01 });
  const body = { checkoutId: randomUUID(), customerId, customerName: "Smoke Buyer", email: "smoke@example.com" };
  const orders = await Promise.all([request("/api/orders", "POST", body), request("/api/orders", "POST", body)]);
  assert.equal(orders[0].id, orders[1].id, "Concurrent checkout retry must return the same order");
  assert.equal(orders[0].totalAmount, product.price * quantity, "Client price must be ignored");
  return orders[0];
}
const paid = await checkout(1);
await until(() => request(`/api/orders/${paid.id}`), o => o.status === "AwaitingPayment", "reserve happy order");
await until(async () => (await fetch(base + `/api/payments/${paid.id}`)).status, s => s === 200, "prepare happy payment");
await request(`/api/payments/${paid.id}/simulate`, "POST", { paid: true });
await request(`/api/payments/${paid.id}/simulate`, "POST", { paid: true });
await until(() => request(`/api/orders/${paid.id}`), o => o.status === "Confirmed", "confirm happy order");
await until(() => request(`/api/basket/${paid.customerId}`), b => b.items.length === 0, "clear paid basket");
await until(() => request(`/api/inventory/${product.id}`), s => s.reservedQuantity === initial.reservedQuantity, "commit paid inventory");
const declined = await checkout(2);
await until(() => request(`/api/orders/${declined.id}`), o => o.status === "AwaitingPayment", "reserve failed order");
await until(async () => (await fetch(base + `/api/payments/${declined.id}`)).status, s => s === 200, "prepare failed payment");
await request(`/api/payments/${declined.id}/simulate`, "POST", { paid: false });
await request(`/api/payments/${declined.id}/simulate`, "POST", { paid: false });
await until(() => request(`/api/orders/${declined.id}`), o => o.status === "Cancelled", "cancel failed order");
const final = await until(() => request(`/api/inventory/${product.id}`),
  s => s.availableQuantity === initial.availableQuantity - 1 && s.reservedQuantity === initial.reservedQuantity, "compensate inventory");
const failedBasket = await request(`/api/basket/${declined.customerId}`);
assert.equal(failedBasket.items[0].quantity, 2, "Failed checkout keeps the basket");
await request(`/api/basket/${declined.customerId}`, "DELETE");
const oversold = await checkout(99);
await until(() => request(`/api/orders/${oversold.id}`), o => o.status === "Cancelled", "reject insufficient inventory");
await request(`/api/basket/${oversold.customerId}`, "DELETE");
const unchanged = await request(`/api/inventory/${product.id}`);
assert.deepEqual(unchanged, final, "Rejected reservation must not change stock");
assert.ok(final.availableQuantity > 0, "Seed more SSD stock before running this test again");
const contenders = await Promise.all([checkout(final.availableQuantity), checkout(final.availableQuantity)]);
const outcomes = await Promise.all(contenders.map(o => until(() => request(`/api/orders/${o.id}`),
  result => ["AwaitingPayment", "Cancelled"].includes(result.status), "competing reservations")));
assert.equal(outcomes.filter(o => o.status === "AwaitingPayment").length, 1, "Only one competing order can reserve the remaining stock");
const winner = outcomes.find(o => o.status === "AwaitingPayment");
await until(async () => (await fetch(base + `/api/payments/${winner.id}`)).status, s => s === 200, "prepare competing payment");
await request(`/api/payments/${winner.id}/simulate`, "POST", { paid: false });
await until(() => request(`/api/inventory/${product.id}`), s => s.availableQuantity === final.availableQuantity && s.reservedQuantity === final.reservedQuantity, "release competing reservation");
for (const contender of contenders) await request(`/api/basket/${contender.customerId}`, "DELETE");
console.log("PASS: catalog, server pricing, concurrent checkout idempotency, reservation, duplicate success/failure, confirmation, basket clearing, compensation, insufficient inventory, concurrent overselling prevention");
console.log("One SSD was consumed by the successful demo order. Re-running this test consumes another.");
