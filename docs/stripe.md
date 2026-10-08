# Stripe test-mode payments

Last verified locally: **2026-10-08**. Hosted Stripe Checkout, a real signed completion webhook, order confirmation, stock commit, basket cleanup, expiry compensation and repeated-event handling passed. See [validation notes](validation.md).

## Start locally on Windows

1. Start the Docker stack with `docker compose up -d --build`.
2. Install the [official Stripe CLI](https://docs.stripe.com/cli/install) and make `stripe` available on PATH. This checkout also supports the executable at ignored `.local/stripe-cli/stripe.exe`.
3. Put your **test secret key** in the ignored root `.env`: `STRIPE_SECRET_KEY=sk_test_...`. Use your own value, not the placeholder. Retain the existing `FRONTEND_URL`, `FRONTEND_PORT` and `GATEWAY_PORT` settings. The current machine uses frontend 3100 and Gateway 8180; repository defaults are 3000 and 8080.
4. From the repository root, run:

   ```powershell
   ./scripts/start-stripe-local.ps1
   ```

The script authenticates the CLI using an environment variable, starts a background listener, captures its signing secret into `.env`, sets `PAYMENT_MODE=Stripe`, and recreates Payment. It forwards the four supported Checkout events to the configured Gateway port. Re-running it replaces only the listener it previously started.

Keep this listener running while testing. After restarting Windows, or replacing the test key, run the script again. Stop it with:

```powershell
./scripts/start-stripe-local.ps1 -Stop
```

Stopping the listener leaves Payment in Stripe mode. To return to Demo, stop the listener, set `PAYMENT_MODE=Demo` in `.env`, then run `docker compose up -d --no-deps payment-api`. Create a fresh Demo order; an existing Stripe Session cannot be settled with Demo controls.

Keys, listener state and logs remain in ignored `.env` and `.local/`. Listener logs contain the webhook signing secret; do not share them unredacted. Rotate a key that has been disclosed in chat or elsewhere, update `.env`, and restart the listener.

## Buyer journey

Sign in, add a product, submit checkout and select **Continue to Stripe** on the order page. Stripe hosts the card form. A publishable key is not required for this redirect integration; the secret key stays in Payment.

Use Stripe's test Visa **4242 4242 4242 4242**, any future expiry and any three-digit CVC. Use fictional billing details and leave saving payment information unchecked. Checkout identifies this account as a sandbox; no real funds are charged. See [Stripe's test details](https://docs.stripe.com/testing).

The app calculates USD totals on the server. Checkout may offer a local currency according to the Stripe account's Adaptive Pricing settings. In the verified test, Stripe retained the authoritative USD 1.23 Session total and recorded VND 33,223 as the separate presentment amount; the signed webhook still matched the USD order.

After payment, the return page polls the backend until the order is confirmed by the signed webhook. A return URL, including `session_id`, is never accepted as payment proof. This follows [Stripe's fulfillment guidance](https://docs.stripe.com/checkout/fulfillment).

Closing Checkout returns to the order with a resume option. It keeps the reservation until the Session expires after 31 minutes. A signed `checkout.session.expired` event cancels the order, releases stock and retains the bag. Orders whose payment is never started expire after 30 minutes.

## Repeatable verification

With the stack and listener running:

```powershell
node --env-file=.env scripts/stripe-checkout-test.mjs start
```

The check uses the existing bootstrap admin to create an isolated customer and product. It checks concurrent Session creation, server totals, return URLs, disabled Demo controls, invalid signatures and account ownership. It explicitly expires one real Stripe Session and waits for the CLI-delivered signed webhook to cancel the order and release stock. It then creates a second Session for browser payment.

Open the new order in the storefront using the test account in ignored `.local/stripe-checkout-test.json`, then resume Stripe Checkout and pay with test details. That temporary file includes the test account password and session cookie; keep it private. Finish with:

```powershell
node --env-file=.env scripts/stripe-checkout-test.mjs verify
```

Verification checks Stripe's paid Session, the confirmed order, committed stock, empty bag and admin order view. It replays the actual completion event with a fresh local signature twice to verify application idempotency. This replay checks our handler; it does not test Stripe Dashboard resend behavior. The script hides the isolated product, clears its remaining available stock and removes credentials/cookies from the state file. Test accounts, orders and audit history remain. If a check fails, retain its state for diagnosis and finish or expire its test Session before cleanup.

The separate `stripe-webhook-test.mjs` fixture harness checks wrong amounts and duplicate expiry/success events without calling Stripe. It now registers authenticated customers. See README for the fixture container instructions; it consumes one seeded mouse per run.

## Boundaries

This implementation deliberately accepts only `sk_test_` keys and test-mode matching Checkout events. It supports card payments, not refunds, shipping, tax or live payments. Local use needs the CLI listener; a shared deployment needs a reachable HTTPS webhook endpoint and its own signing secret, plus delivery monitoring and payment reconciliation. Broker-outage recovery and the GitHub Actions run for these changes have not been verified in this local Stripe check.
