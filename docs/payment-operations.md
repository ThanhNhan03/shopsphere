# Payment operations

The administrator Payments page at `/admin/payments` lists the latest 100 stored payments. When Payment is in Stripe mode, an administrator can reconcile a pending or processing Checkout Session with Stripe. The backend retrieves the session using the server-side secret key and verifies the order reference, session ID, amount, currency and `livemode` before applying a confirmed/cancelled transition through the existing outbox.

Reconciliation is recorded with the administrator identity and can be repeated safely. A mismatch remains for manual review. This feature does not issue refunds or change provider-side sessions. Use the [Stripe runbook](stripe.md) for setup and operation details.

For a live deployment, keep the key and webhook secret in a secret store, point Stripe at the deployed HTTPS webhook route, verify event delivery and rehearse alerting/reconciliation. No live Stripe credentials, deployment URL or live-money test were available here; only code/build/model validation and prior Stripe test-mode verification have been performed.
