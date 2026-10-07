// Run with: node --env-file=.env scripts/admin-smoke-test.mjs
// Uses one isolated customer/product/order. Archives the product and clears its remaining stock.
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
const base = process.env.API_URL || `http://localhost:${process.env.GATEWAY_PORT || 8080}`;
const origin = process.env.FRONTEND_URL || `http://localhost:${process.env.FRONTEND_PORT || 3000}`;
function accountClient() {
  let cookie = "";
  return async (path, method = "GET", body, expected = 200, extraHeaders = {}) => {
    const form = body instanceof FormData;
    const response = await fetch(base + path, { method, headers: { ...(form ? {} : { "Content-Type": "application/json" }), Origin: origin, Cookie: cookie, ...extraHeaders }, body: body === undefined ? undefined : form ? body : JSON.stringify(body) });
    for (const value of response.headers.getSetCookie()) if (value.startsWith("shopsphere-session=")) cookie = value.split(";")[0];
    const result = await response.json().catch(() => null);
    assert.equal(response.status, expected, `${method} ${path}: ${response.status} ${JSON.stringify(result)}`);
    return result;
  };
}
const admin = accountClient(), customer = accountClient(), anonymous = accountClient();
await anonymous("/api/admin/products", "GET", undefined, 401);
assert.ok(process.env.ADMIN_BOOTSTRAP_PASSWORD, "Run scripts/setup-local-admin.ps1 first.");
await admin("/api/auth/login", "POST", { email: process.env.ADMIN_BOOTSTRAP_EMAIL || "admin@shopsphere.local", password: process.env.ADMIN_BOOTSTRAP_PASSWORD }, 204);
assert.equal((await admin("/api/auth/session")).user.isAdmin, true);
await admin("/api/admin/products", "POST", {}, 403, { Origin: "https://invalid.example" });
const email = `smoke-${randomUUID()}@example.com`, password = randomUUID();
await customer("/api/auth/register", "POST", { name: "Administration smoke buyer", email, password, isAdmin: true }, 204);
const session = await customer("/api/auth/session");
assert.equal(session.user.isAdmin, false);
await customer("/api/admin/products", "GET", undefined, 403);
await customer("/api/admin/products", "GET", undefined, 403, { "X-Admin-Email": process.env.ADMIN_BOOTSTRAP_EMAIL });
await customer("/api/auth/register", "POST", { name: "Duplicate", email: email.toUpperCase(), password }, 409);
await anonymous("/api/auth/login", "POST", { email, password: "incorrect-password" }, 401);
await customer("/api/auth/logout", "POST", undefined, 204);
assert.equal((await customer("/api/auth/session")).user, null);
await customer("/api/auth/login", "POST", { email: email.toUpperCase(), password }, 204);
const customerId = (await customer("/api/auth/session")).user.customerId;
let product = await admin("/api/admin/products", "POST", { name: `Smoke product ${randomUUID().slice(0,8)}`, description: "Isolated administration smoke test", price: 12.34, brand: "Smoke", category: "Testing", isActive: false });
const productPath = `/api/admin/products/${product.id}`;
const write = (p, isActive = p.isActive) => ({ name: p.name, description: p.description, price: p.price, brand: p.brand, category: p.category, isActive, version: p.version });
await anonymous(`/api/products/${product.id}`, "GET", undefined, 404);
const invalid = new FormData(); invalid.set("version", String(product.version)); invalid.set("image", new Blob(["<svg/>"], { type: "image/svg+xml" }), "bad.svg");
await admin(productPath + "/image", "POST", invalid, 400);
const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/a9sAAAAASUVORK5CYII=", "base64");
async function upload() {
  const form = new FormData(); form.set("version", String(product.version)); form.set("image", new Blob([png], { type: "image/png" }), "photo.png");
  product = await admin(productPath + "/image", "POST", form);
}
await upload();
const replacedUrl = product.imageUrl;
await upload();
await anonymous(replacedUrl, "GET", undefined, 404);
const photo = await fetch(base + product.imageUrl);
assert.equal(photo.status, 200); assert.equal(photo.headers.get("content-type"), "image/png");
assert.equal(photo.headers.get("x-content-type-options"), "nosniff"); assert.deepEqual(Buffer.from(await photo.arrayBuffer()), png);
const privateObject = await fetch(`http://localhost:${process.env.MINIO_PORT || 9000}/${process.env.S3_BUCKET || "shopsphere-products"}/products/${product.imageUrl.split("/").at(-1)}`);
assert.equal(privateObject.status, 403, "MinIO bucket must not allow anonymous access");
const inventoryPath = `/api/admin/inventory/${product.id}/adjustments`;
let stock = await admin(inventoryPath, "POST", { delta: 2, expectedAvailable: 0, reason: "Smoke restock" }, 200, { "X-Admin-Email": "forged@example.com" });
assert.equal(stock.availableQuantity, 2);
await admin(inventoryPath, "POST", { delta: 1, expectedAvailable: 0, reason: "Stale write" }, 409);
const history = await admin(inventoryPath);
assert.equal(history.length, 1); assert.equal(history[0].performedBy, process.env.ADMIN_BOOTSTRAP_EMAIL || "admin@shopsphere.local");
const stale = write(product);
product = await admin(productPath, "PUT", write(product, true));
await admin(productPath, "PUT", stale, 409);
assert.equal((await anonymous(`/api/products/${product.id}`)).imageUrl, product.imageUrl);
await customer(`/api/basket/${customerId}/items`, "POST", { productId: product.id, quantity: 1, unitPrice: 0.01 });
product = await admin(productPath, "PUT", write(product, false));
assert.equal((await customer(`/api/basket/${customerId}`)).items[0].isAvailable, false);
await customer("/api/orders", "POST", { checkoutId: randomUUID(), customerId, customerName: "Smoke Buyer", email }, 400);
product = await admin(productPath, "PUT", write(product, true));
const checkout = { checkoutId: randomUUID(), customerId, customerName: "Smoke Buyer", email };
const [first, retry] = await Promise.all([customer("/api/orders", "POST", checkout, 201), customer("/api/orders", "POST", checkout, 201)]);
assert.equal(first.id, retry.id); assert.equal(first.totalAmount, product.price);
async function until(path, predicate, label) {
  for (let n = 0; n < 30; n++) {
    const value = await customer(path); if (predicate(value)) return value;
    await new Promise(r => setTimeout(r, 1000));
  }
  throw new Error(`Timed out: ${label}`);
}
await until(`/api/orders/${first.id}`, o => o.status === "AwaitingPayment", "reserve order");
let payment;
for (let n = 0; n < 30; n++) {
  // Payment projection can arrive just after the ordering event.
  try { payment = await customer(`/api/payments/${first.id}`); break; } catch { await new Promise(r => setTimeout(r, 1000)); }
}
assert.ok(payment, "Payment projection is available");
if (payment.mode === "Demo") {
  await customer(`/api/payments/${first.id}/simulate`, "POST", { paid: true });
  await until(`/api/orders/${first.id}`, o => o.status === "Confirmed", "confirm demo order");
  assert.equal((await admin(`/api/admin/orders/${first.id}`)).status, "Confirmed");
} else throw new Error("Run this smoke test with PAYMENT_MODE=Demo.");
await anonymous(`/api/orders/${first.id}`, "GET", undefined, 401);
const page = await admin(`/api/admin/orders?q=${first.id}&page=1`);
assert.equal(page.total, 1); assert.equal(page.items[0].id, first.id);
assert.ok((await admin("/api/admin/orders/summary")).revenue >= product.price);
product = await admin(productPath, "PUT", write(product, false));
stock = (await admin("/api/admin/inventory")).find(s => s.productId === product.id);
if (stock.availableQuantity > 0) await admin(inventoryPath, "POST", { delta: -stock.availableQuantity, expectedAvailable: stock.availableQuantity, reason: "Smoke cleanup" });
await customer(`/api/basket/${customerId}`, "DELETE", undefined, 204);
console.log("PASS: registration/login/logout, admin authorization, Origin checks, private MinIO upload/read/replacement, product visibility/concurrency, stock history/concurrency, authenticated checkout and confirmed order reporting.");
console.log("One isolated customer and confirmed demo order remain in history; the test product is hidden with no available stock.");
