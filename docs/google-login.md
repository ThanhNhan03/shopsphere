# Google / Gmail sign-in

The storefront supports optional Google OAuth alongside [local email/password accounts](admin.md). Both use an HttpOnly, eight-hour server cookie. Gmail accounts use the Google sign-in button; ShopSphere does not request access to the user's mailbox.

## Local configuration

1. In Google Cloud / Google Auth Platform, create or select a project and configure the OAuth consent screen. If the app is in Testing, add your Gmail address as a test user.
2. Create an OAuth client of type **Web application**.
3. Add the authorized redirect URI exactly: `http://localhost:3000/api/auth/google/callback`.
4. Put `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` in the Git-ignored root `.env`. Do not commit or paste the secret in chat.
5. Recreate Gateway: `docker compose up -d gateway`.
6. Open `http://localhost:3000/login`, choose **Continue with Google**, then check your name appears in the header. Add an item, create an order, sign out and sign back in to verify the same bag/account is restored.

When changing the frontend port or hostname, update `FRONTEND_URL` and the Google redirect URI together. Use HTTPS outside localhost.

## Access rules

- Every Google sign-in requests `prompt=select_account`: choose an existing account or **Use another account**. ShopSphere logout ends the application session, while the browser's Google session remains signed in.

- Browsing products is public. A Google or local account session is required for basket, order and payment operations through Gateway.
- Basket IDs are derived from Google's stable account ID, rather than email addresses or browser-generated IDs.
- A verified Google `sub` is SHA-256 hashed and stored with the account in Gateway's Accounts database; the profile name, HTTPS picture URL and last sign-in time are refreshed on login. The raw provider subject and OAuth tokens are not stored. A verified email can link to an existing account; an unverified or conflicting identity is rejected.
- Gateway checks basket ownership, checkout customer ID, and order ownership before allowing order/payment requests. Google tokens and secrets are not exposed to the browser.
- State-changing storefront requests must carry the configured storefront Origin. The Stripe webhook retains its signature-based protection.
- Data Protection keys persist in the `auth-keys` Docker volume so sessions survive Gateway container replacement.
- Existing anonymous baskets/orders are not automatically transferred to signed-in users.

## Request limits

Gateway uses sliding 60-second windows, with no request queue:

| Traffic | Limit |
| --- | --- |
| API reads | 120 per signed-in account, or anonymous connection IP |
| API writes | 30 per signed-in account, or anonymous connection IP |
| Google sign-in starts / local login / registration | 10 per connection IP, shared bucket |
| OAuth callbacks | 30 per connection IP |
| Stripe webhook | 120 per connection IP, in a separate bucket |
| Overall API connection ceiling | 600 per connection IP; Stripe has a separate ceiling |

Rejected requests return HTTP 429 with `Retry-After` and a readable JSON error. Health checks are excluded. Configure `RATE_LIMIT_READ`, `RATE_LIMIT_WRITE` and `RATE_LIMIT_LOGIN` in the root `.env` and recreate Gateway to change the main quotas. Next.js preserves the retry header.

Anonymous requests forwarded by the local Next.js server share that server's connection IP quota. Client-supplied forwarding headers cannot change the bucket. Counters are in memory in each Gateway instance and reset on restart. Multiple Gateway replicas or public deployment need a shared limiter (for example Redis) and a trusted ingress for original client addresses.
- Internal services must remain private to the Docker network. Authorization is enforced at Gateway, not independently inside each service.

## Validation limits

Verified locally on 2026-10-07 with the configured Google OAuth client: real Google sign-in returns to the storefront, the header displays the account name, reload retains the session, basket writes succeed, checkout pre-fills account name/email, logout removes access, and signing back in restores the same basket and return URL. The walkthrough left one Keychron K2 in the signed-in account's bag; no order or payment was created.

The account-persistence implementation includes an EF migration and model tests. The current environment did not have a running Docker engine, so applying that migration and verifying a Google callback writes/updates an account row remain pending runtime checks. `scripts/admin-smoke-test.mjs` checks real local-account sessions, administrator authorization, and Origin checks against the Docker stack. A second real Google account has not been tested.

Without a configured OAuth client, the login screen offers email/password login and registration. Gateway must own `/auth-keys` as the non-root application user; the Dockerfile prepares this directory with permissions 700. Existing root-owned volumes need their owner corrected before OAuth can protect state or session cookies.

The original anonymous `scripts/smoke-test.mjs` and synthetic webhook script do not authenticate to Gateway. They need an authenticated test-session harness before they can run through the now-protected public endpoints. Do not disable access checks to run those scripts.

References: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/social-without-identity?view=aspnetcore-10.0 and https://developers.google.com/identity/openid-connect/openid-connect
