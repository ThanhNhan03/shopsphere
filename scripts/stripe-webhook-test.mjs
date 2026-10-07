// Synthetic signed Stripe Sessions; never calls Stripe. Run the fixture container from README first.
import assert from "node:assert/strict";
import { randomUUID, createHmac } from "node:crypto";
import { execFileSync } from "node:child_process";
const gateway = process.env.API_URL || "http://localhost:8080";
const paymentBase = process.env.PAYMENT_FIXTURE_URL || "http://localhost:5105";
const secret = "whsec_fixture_only";
function sql(statement) {
  return execFileSync("docker", ["compose", "exec", "-T", "postgres", "sh", "-c",
    'exec psql -U "$POSTGRES_USER" -d payment_db -v ON_ERROR_STOP=1 -tAc "$1"', "sh", statement], { encoding: "utf8" }).trim();
}
async function api(path, method = "GET", body) {
  const r = await fetch(gateway + path, { method, headers: { "Content-Type": "application/json" }, body: body && JSON.stringify(body) });
  assert.ok(r.ok, `${path} returned ${r.status}`);
  return r.status === 204 ? null : r.json();
}
async function until(read, predicate, label) {
  for (let i = 0; i < 60; i++) {
    const value = await read();
    if (predicate(value)) return value;
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error("Timed out: " + label);
}
async function webhook(event, valid = true) {
  const payload = JSON.stringify(event); const timestamp = Math.floor(Date.now() / 1000);
  const signature = createHmac("sha256", secret).update(`${timestamp}.${payload}`).digest("hex");
  return fetch(paymentBase + "/api/payments/webhooks/stripe", { method: "POST",
    headers: { "Content-Type": "application/json", "Stripe-Signature": `t=${timestamp},v1=${valid ? signature : "invalid"}` },
    body: payload });
}
const product = await api("/api/products/00000000-0000-0000-0000-000000000003");
const initial = await api(`/api/inventory/${product.id}`);
async function makeOrder() {
  const customerId = randomUUID();
  await api(`/api/basket/${customerId}/items`, "POST", { productId: product.id, quantity: 1 });
  const order = await api("/api/orders", "POST", { checkoutId: randomUUID(), customerId, customerName: "Webhook Buyer", email: "fixture@example.com" });
  await until(() => api(`/api/orders/${order.id}`), o => o.status === "AwaitingPayment", "reserve fixture order");
  await until(async () => (await fetch(gateway + `/api/payments/${order.id}`)).status, status => status === 200, "create pending payment");
  const sessionId = "cs_test_" + order.id.replaceAll("-", "");
  sql(`UPDATE "Payments" SET "StripeSessionId" = '${sessionId}', "Status" = 1 WHERE "OrderId" = '${order.id}'`);
  return { ...order, sessionId };
}
function event(order, type = "checkout.session.completed", amount = order.totalAmount * 100) {
  return { id: "evt_" + randomUUID().replaceAll("-", ""), object: "event", api_version: "2026-09-30.clover",
    created: Math.floor(Date.now() / 1000), livemode: false, type, data: { object: {
      id: order.sessionId, object: "checkout.session", client_reference_id: order.id, currency: "usd",
      amount_total: amount, payment_status: type === "checkout.session.completed" ? "paid" : "unpaid", payment_intent: "pi_test_fixture",
    } } };
}
const paid = await makeOrder(); const success = event(paid);
assert.equal((await webhook(success, false)).status, 400, "Invalid signatures must be rejected");
assert.equal((await webhook(event(paid, "checkout.session.completed", 1))).status, 400, "Wrong amount must be rejected");
assert.equal((await webhook(success)).status, 200);
assert.equal((await webhook(success)).status, 200, "Duplicate signed event must be acknowledged");
await until(() => api(`/api/orders/${paid.id}`), o => o.status === "Confirmed", "signed payment success");
assert.equal(sql(`SELECT count(*) FROM "StripeReceipts" WHERE "EventId" = '${success.id}'`), "1");
const failed = await makeOrder(); const expired = event(failed, "checkout.session.expired");
assert.equal((await webhook(expired)).status, 200);
assert.equal((await webhook(expired)).status, 200);
await until(() => api(`/api/orders/${failed.id}`), o => o.status === "Cancelled", "signed payment expiry");
await until(() => api(`/api/inventory/${product.id}`), s => s.availableQuantity === initial.availableQuantity - 1 && s.reservedQuantity === initial.reservedQuantity, "signed compensation");
assert.equal(sql(`SELECT count(*) FROM "StripeReceipts" WHERE "EventId" = '${expired.id}'`), "1");
await api(`/api/basket/${failed.customerId}`, "DELETE");
console.log("PASS: bad signature, mismatched amount, signed success, duplicate Stripe receipt, signed expiry, duplicate failure, compensation");
console.log("Synthetic fixtures only; external Stripe Checkout is unverified. One mouse was consumed.");
