// Real Stripe test-mode Sessions and CLI webhooks. Never run against a live key.
// Start: node --env-file=.env scripts/stripe-checkout-test.mjs start
// Pay the ignored .local/stripe-checkout-test.json checkoutUrl in a browser, then run with verify.
import assert from "node:assert/strict";
import { randomUUID, createHmac } from "node:crypto";
import fs from "node:fs/promises";
const base = process.env.API_URL || `http://localhost:${process.env.GATEWAY_PORT || 8080}`;
const origin = process.env.FRONTEND_URL || `http://localhost:${process.env.FRONTEND_PORT || 3000}`;
const stateFile = new URL("../.local/stripe-checkout-test.json", import.meta.url);
assert.ok(process.env.STRIPE_SECRET_KEY?.startsWith("sk_test_"), "A Stripe test key is required.");
assert.ok(process.env.STRIPE_WEBHOOK_SECRET?.startsWith("whsec_"), "Start scripts/start-stripe-local.ps1 first.");
function client(initialCookie = "") {
  let cookie = initialCookie;
  const request = async (path, method = "GET", body, expected = 200) => {
    const response = await fetch(base + path, { method, headers: { "Content-Type": "application/json", Origin: origin, Cookie: cookie }, body: body === undefined ? undefined : JSON.stringify(body) });
    for (const value of response.headers.getSetCookie()) if (value.startsWith("shopsphere-session=")) cookie = value.split(";")[0];
    const result = await response.json().catch(() => null);
    assert.ok([expected].flat().includes(response.status), `${method} ${path} returned ${response.status}: ${result?.detail || "unexpected status"}`);
    return result;
  };
  request.cookie = () => cookie;
  return request;
}
async function until(read, predicate, label) {
  for (let n = 0; n < 60; n++) {
    const value = await read(); if (predicate(value)) return value;
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  throw new Error(`Timed out: ${label}. Keep the Stripe listener running.`);
}
async function stripe(path, method = "GET") {
  const response = await fetch("https://api.stripe.com/v1" + path, { method, headers: { Authorization: `Bearer ${process.env.STRIPE_SECRET_KEY}` } });
  const result = await response.json();
  assert.ok(response.ok, `Stripe returned ${response.status} (${result.error?.code || result.error?.type || "request failed"}); provider messages suppressed.`);
  assert.notEqual(result.livemode, true, "Live Stripe objects are prohibited in this check.");
  return result;
}
const admin = client();
await admin("/api/auth/login", "POST", { email: process.env.ADMIN_BOOTSTRAP_EMAIL || "admin@shopsphere.local", password: process.env.ADMIN_BOOTSTRAP_PASSWORD }, 204);
const phase = process.argv[2];
if (phase === "start") {
  const customer = client();
  const email = `stripe-${randomUUID()}@example.com`;
  const password = randomUUID();
  await customer("/api/auth/register", "POST", { name: "Stripe test buyer", email, password }, 204);
  const customerId = (await customer("/api/auth/session")).user.customerId;
  let product = await admin("/api/admin/products", "POST", { name: `Stripe check ${randomUUID().slice(0, 8)}`, brand: "Testing", category: "Testing", description: "Isolated Stripe integration check", price: 1.23, isActive: true });
  const stockPath = `/api/admin/inventory/${product.id}/adjustments`;
  await admin(stockPath, "POST", { delta: 2, expectedAvailable: 0, reason: "Stripe verification stock" });
  await customer(`/api/basket/${customerId}/items`, "POST", { productId: product.id, quantity: 1 });
  async function orderAndSession() {
    const order = await customer("/api/orders", "POST", { checkoutId: randomUUID(), customerId, customerName: "Stripe test buyer", email }, 201);
    await until(() => customer(`/api/orders/${order.id}`), value => value.status === "AwaitingPayment", "stock reservation");
    await until(() => customer(`/api/payments/${order.id}`, "GET", undefined, [200, 404]), value => value?.mode === "Stripe", "Stripe payment projection");
    const [payment, retried] = await Promise.all([customer("/api/payments/checkout-session", "POST", { orderId: order.id }), customer("/api/payments/checkout-session", "POST", { orderId: order.id })]);
    assert.equal(payment.mode, "Stripe"); assert.equal(payment.checkoutUrl, retried.checkoutUrl, "Checkout retries reuse the same Session");
    const url = new URL(payment.checkoutUrl);
    assert.equal(url.hostname, "checkout.stripe.com");
    const sessionId = url.pathname.split("/").at(-1);
    assert.ok(sessionId.startsWith("cs_test_"));
    const session = await stripe(`/checkout/sessions/${sessionId}`);
    assert.equal(session.amount_total, 123); assert.equal(session.currency, "usd");
    assert.equal(session.client_reference_id, order.id); assert.equal(session.metadata.orderId, order.id);
    assert.ok(session.success_url.startsWith(origin + "/payment/success?"));
    assert.ok(session.cancel_url.startsWith(origin + `/orders/${order.id}?cancelled=1`));
    await customer(`/api/payments/${order.id}/simulate`, "POST", { paid: true }, 404);
    return { order, sessionId, checkoutUrl: payment.checkoutUrl };
  }
  const expired = await orderAndSession();
  await stripe(`/checkout/sessions/${expired.sessionId}/expire`, "POST");
  await until(() => customer(`/api/orders/${expired.order.id}`), value => value.status === "Cancelled", "real Stripe expired webhook");
  await until(() => admin("/api/admin/inventory"), stocks => stocks.some(s => s.productId === product.id && s.availableQuantity === 2 && s.reservedQuantity === 0), "expiry compensation");
  assert.equal((await customer(`/api/basket/${customerId}`)).items[0].quantity, 1);
  const paid = await orderAndSession();
  const badSignature = await fetch(base + "/api/payments/webhooks/stripe", { method: "POST", headers: { "Content-Type": "application/json", "Stripe-Signature": "t=1,v1=invalid" }, body: "{}" });
  assert.equal(badSignature.status, 400);
  const stranger = client();
  await stranger(`/api/orders/${paid.order.id}`, "GET", undefined, 401);
  await stranger("/api/auth/register", "POST", { name: "Other Stripe buyer", email: `stripe-other-${randomUUID()}@example.com`, password: randomUUID() }, 204);
  // Gateway conceals another customer's order with 404 rather than disclosing its existence.
  await stranger(`/api/payments/${paid.order.id}`, "GET", undefined, 404);
  await fs.mkdir(new URL("../.local/", import.meta.url), { recursive: true });
  await fs.writeFile(stateFile, JSON.stringify({ cookie: customer.cookie(), email, password, customerId, productId: product.id, orderId: paid.order.id, expiredOrderId: expired.order.id, sessionId: paid.sessionId, checkoutUrl: paid.checkoutUrl }, null, 2));
  console.log("PASS: real Stripe Session creation/retry, test-only mode, return URLs, signed expiry webhook, stock release, basket retention, invalid signature, and customer ownership.");
  console.log("Open checkoutUrl from ignored .local/stripe-checkout-test.json, pay with a Stripe test card, then run verify.");
} else if (phase === "verify") {
  const state = JSON.parse(await fs.readFile(stateFile, "utf8"));
  assert.ok(state.cookie, "Run start before verification.");
  const customer = client(state.cookie);
  const session = await stripe(`/checkout/sessions/${state.sessionId}`);
  assert.equal(session.payment_status, "paid", "Complete the hosted test Checkout first.");
  await until(() => customer(`/api/orders/${state.orderId}`), value => value.status === "Confirmed", "real Stripe paid webhook");
  const payment = await customer(`/api/payments/${state.orderId}`);
  assert.equal(payment.status, "Completed");
  const stocks = await until(() => admin("/api/admin/inventory"), values => values.some(s => s.productId === state.productId && s.availableQuantity === 1 && s.reservedQuantity === 0), "stock commit");
  assert.equal((await customer(`/api/basket/${state.customerId}`)).items.length, 0);
  const events = await stripe("/events?type=checkout.session.completed&limit=50");
  const event = events.data.find(e => e.data.object.id === state.sessionId);
  assert.ok(event, "Real Checkout completion event exists in Stripe.");
  // Replay the actual Stripe event with a fresh local signature to check business idempotency.
  const payload = JSON.stringify(event), timestamp = Math.floor(Date.now() / 1000);
  const signature = createHmac("sha256", process.env.STRIPE_WEBHOOK_SECRET).update(`${timestamp}.${payload}`).digest("hex");
  for (let n = 0; n < 2; n++) {
    const response = await fetch(base + "/api/payments/webhooks/stripe", { method: "POST", headers: { "Content-Type": "application/json", "Stripe-Signature": `t=${timestamp},v1=${signature}` }, body: payload });
    assert.equal(response.status, 200);
  }
  assert.deepEqual(await customer(`/api/payments/${state.orderId}`), payment);
  const afterReplay = (await admin("/api/admin/inventory")).find(s => s.productId === state.productId);
  assert.deepEqual(afterReplay, stocks.find(s => s.productId === state.productId));
  assert.equal((await admin(`/api/admin/orders/${state.orderId}`)).status, "Confirmed");
  const product = await admin(`/api/admin/products/${state.productId}`);
  await admin(`/api/admin/products/${state.productId}`, "PUT", { ...product, isActive: false });
  await admin(`/api/admin/inventory/${state.productId}/adjustments`, "POST", { delta: -afterReplay.availableQuantity, expectedAvailable: afterReplay.availableQuantity, reason: "Stripe verification cleanup" });
  await fs.writeFile(stateFile, JSON.stringify({ orderId: state.orderId, expiredOrderId: state.expiredOrderId, verifiedAt: new Date().toISOString(), result: "Passed" }, null, 2));
  console.log("PASS: hosted Stripe test payment, real signed completion webhook, confirmed order, stock commit, basket cleanup, admin reporting, and repeat-event idempotency.");
  console.log("The isolated product is hidden with zero available stock; test customer/order and audit history remain.");
} else {
  throw new Error("Use start or verify as the final argument.");
}
