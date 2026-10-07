// Exercise the real RabbitMQ -> Catalog consumer with an isolated projection ID.
// No product, Inventory stock, user bag or payment is modified.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
const productId = randomUUID();
function sql(statement) {
  return execFileSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c',
    'exec psql -X -qAt -U "$POSTGRES_USER" -d catalog_db -v ON_ERROR_STOP=1'],
    { input: statement, encoding: 'utf8' }).trim();
}
function brokerEnv(key) {
  return execFileSync('docker', ['compose', 'exec', '-T', 'rabbitmq', 'printenv', key], { encoding: 'utf8' }).trim();
}
const auth = Buffer.from(`${brokerEnv('RABBITMQ_DEFAULT_USER')}:${brokerEnv('RABBITMQ_DEFAULT_PASS')}`).toString('base64');
const broker = process.env.RABBITMQ_MANAGEMENT_URL || 'http://localhost:15672';
async function publish(version, availableQuantity) {
  const messageId = randomUUID();
  const envelope = { messageId, messageType: ['urn:message:ShopSphere.Contracts:StockAvailabilityChangedIntegrationEvent'],
    message: { eventId: messageId, correlationId: productId, occurredAt: new Date().toISOString(), productId, version, availableQuantity } };
  const response = await fetch(`${broker}/api/exchanges/%2F/${encodeURIComponent('ShopSphere.Contracts:StockAvailabilityChangedIntegrationEvent')}/publish`,
    { method: 'POST', headers: { Authorization: `Basic ${auth}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ properties: { content_type: 'application/vnd.masstransit+json' },
        routing_key: '', payload: JSON.stringify(envelope), payload_encoding: 'string' }), signal: AbortSignal.timeout(15000) });
  assert.equal(response.status, 200);
  assert.equal((await response.json()).routed, true);
  for (let i = 0; i < 80; i++) {
    if (sql(`SELECT EXISTS (SELECT 1 FROM "InboxState" WHERE "MessageId" = '${messageId}' AND "Consumed" IS NOT NULL);`) === 't') return;
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Catalog did not commit the projection event');
}
function state() {
  return JSON.parse(sql(`SELECT json_build_object('quantity',"AvailableQuantity",'version',"Version") FROM "ProductAvailability" WHERE "ProductId"='${productId}';`));
}
try {
  await publish(20, 0);
  assert.deepEqual(state(), { quantity: 0, version: 20 });
  await publish(19, 99);
  assert.deepEqual(state(), { quantity: 0, version: 20 }, 'Delayed older event must not resurrect sold-out stock');
  await publish(20, 99);
  assert.deepEqual(state(), { quantity: 0, version: 20 }, 'Repeated version must not overwrite the projection');
  await publish(21, 2);
  assert.deepEqual(state(), { quantity: 2, version: 21 });
  console.log('PASS: real consumer applies newer availability and ignores delayed/duplicate versions.');
} finally {
  sql(`DELETE FROM "ProductAvailability" WHERE "ProductId"='${productId}';`);
}
