# Administration, accounts, and product photos

## Local setup

Preserve your existing root `.env`. Run `./scripts/setup-local-admin.ps1`, then `docker compose up -d --build`. For an already running PostgreSQL instance, `docker compose run --rm postgres-init` can create the additional `identity_db` without removing existing databases or volumes.

After switching or merging branches, rebuild the affected images with `docker compose up -d --build`. Restarting an existing container keeps its old image and environment; editing `.env` alone does not update a running container. Use `docker compose up -d` to apply environment changes to existing images.

The setup script sets `ADMIN_BOOTSTRAP_EMAIL=admin@shopsphere.local` and generates a random password when those settings are missing. Account details are saved in `.local/admin-account.txt`; both that directory and `.env` are ignored by Git and the Docker build context. Gateway seeds the account once after migrations. It stores an ASP.NET Core PasswordHasher hash, never plaintext. Restarting containers or changing the bootstrap password does not reset an existing account. An existing customer with the bootstrap email causes a clear startup failure rather than silently granting permissions.

Sign in at `/login` and open `/admin`. Customers can register at `/register` with a name, email, and password of 10–128 characters. Registration also starts a session. Emails are trimmed and normalized with a database unique constraint. Unknown accounts and incorrect passwords return the same login error. Registration cannot grant administrator permissions. Google and local identities remain separate; matching email addresses do not merge accounts or baskets.

Optional `ADMIN_EMAILS` accepts comma-separated verified Google email addresses. Local registration cannot satisfy the verified Google email rule. HttpOnly cookies expire after eight hours. Login/registration share the configured per-IP login limit; writes require the configured storefront Origin. Changing the frontend port also requires changing `FRONTEND_URL`.

## Management screens

- **Overview:** persisted catalog/stock/order totals, confirmed revenue, and low-stock products (five available units or fewer).
- **Products:** create and edit name, brand, category, description, price, visibility, and photo. New UI entries default to hidden. Product versions prevent stale edits and uploads from overwriting newer changes. Hide products to retain their order history; a hidden product in a customer's basket is still removable but cannot be checked out.
- **Inventory:** add/remove available units with a reason and a recorded administrator identity. Reserved units are protected. Row locking and expected availability prevent concurrent changes from silently losing updates. Recent history shows the latest 30 adjustments.
- **Orders:** search ID/customer/email, filter status, page through orders, and inspect line items, totals, dates, and cancellation reasons. Status changes continue to follow inventory/payment events.

Product and inventory screens use server-side search, visibility filters, and pagination (50 products per page, at most 100). Stock reads are limited to the products on the current page. Overview counts cover the complete catalog, with a preview of at most ten low-stock products from the Inventory availability projection.

## MinIO storage

Compose builds MinIO from a pinned commit of the [official source](https://github.com/minio/minio), following its source-only distribution instructions. The container runs as a non-root user, exposes only loopback ports, and persists objects in `minio-data`. Initial compilation can take several minutes.

`MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` configure the local S3 credentials and console login. `MINIO_PORT` defaults to 9000; `MINIO_CONSOLE_PORT` defaults to 9001. Catalog uses the internal endpoint `http://minio:9000`, path-style S3 requests, and `S3_BUCKET` (default `shopsphere-products`). The bucket is created on first upload and remains private. Browser requests use `/api/media/<object-key>` through Next.js/Gateway/Catalog; S3 credentials are never sent to the browser.

Photos accept PNG/JPEG/WebP up to 5 MB. The backend checks MIME type against file signatures, generates object names, serves images with `nosniff` and immutable caching, and rejects SVG. Replacing a photo saves the new reference before removing the old object; failed database saves attempt to remove the unreferenced upload. A failed upload after saving product details is shown explicitly and can be retried.

Back up PostgreSQL and the MinIO volume together. Keeping image objects without catalog references, or catalog references without objects, is incomplete. Outside the local demo, use HTTPS, distinct least-privilege S3 credentials, managed secrets, coordinated migrations, and a shared rate limiter. Account recovery/email verification and role-management workflows remain future work.

## Verification

```powershell
dotnet test ShopSphere.sln -c Release
node --env-file=.env scripts/admin-smoke-test.mjs
```

Run the smoke check only against a local stack in Demo payment mode. It creates isolated test data and verifies login/session/logout, administrator denial for customers, Origin protection, private S3 upload/read/replacement, stale product and stock writes, hidden-product checkout protection, server pricing, idempotent checkout, and admin order reporting. It hides the product and removes its remaining available units, preserving the customer/order and audit history.
