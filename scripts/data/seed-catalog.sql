-- Deterministic, resumable fixture. Original six products/stock are untouched.
-- psql variables first/last select a bounded batch; never build a giant migration.
\set ON_ERROR_STOP on
BEGIN;
SELECT pg_advisory_xact_lock(141, 1000000);
WITH hashes AS (
    SELECT n,
        ('x' || substr(md5(n::text || ':device'), 1, 8))::bit(32)::bigint AS device_hash,
        ('x' || substr(md5(n::text || ':brand'), 1, 8))::bit(32)::bigint AS brand_hash,
        ('x' || substr(md5(n::text || ':price'), 1, 8))::bit(32)::bigint AS price_hash,
        ('x' || substr(md5(n::text || ':spec'), 1, 8))::bit(32)::bigint AS spec_hash
    FROM generate_series(:first::integer, :last::integer) n
), device AS (
    SELECT *, CASE
        WHEN device_hash % 1000 < 300 THEN 0
        WHEN device_hash % 1000 < 500 THEN 1
        WHEN device_hash % 1000 < 650 THEN 2
        WHEN device_hash % 1000 < 800 THEN 3
        WHEN device_hash % 1000 < 920 THEN 4
        ELSE 5 END AS kind
    FROM hashes
), products AS (
    SELECT d.*, t.*, brands[1 + (brand_hash % array_length(brands, 1))::integer] AS brand
    FROM device d JOIN (VALUES
        (0, 'Laptops', 'laptop', 'Notebook', ARRAY['Apple','Dell','Lenovo','HP','ASUS','Acer'], 39900, 260000, 'Portable laptop with SSD storage and an IPS display.'),
        (1, 'Audio', 'headphones', 'Wireless Headphones', ARRAY['Sony','Bose','JBL','Sennheiser','Audio-Technica'], 2900, 57000, 'Wireless headphones with USB-C charging and adjustable fit.'),
        (2, 'Accessories', 'mouse', 'Wireless Mouse', ARRAY['Logitech','Razer','Microsoft','Corsair'], 900, 18000, 'Wireless mouse with adjustable sensitivity and ergonomic grip.'),
        (3, 'Accessories', 'keyboard', 'Mechanical Keyboard', ARRAY['Keychron','Logitech','Corsair','Razer'], 2900, 27000, 'Mechanical keyboard with USB-C connectivity and replaceable keycaps.'),
        (4, 'Monitors', 'monitor', 'Desktop Monitor', ARRAY['Dell','LG','Samsung','ASUS','Acer'], 9900, 160000, 'Desktop IPS monitor with HDMI input and an adjustable stand.'),
        (5, 'Storage', 'ssd', 'Portable SSD', ARRAY['Samsung','Western Digital','Kingston','Crucial'], 3900, 56000, 'Portable solid-state storage with a USB-C interface.')
    ) t(kind_id, category, icon, model, brands, base_cents, span_cents, description) ON d.kind = t.kind_id
)
INSERT INTO "Products" ("Id", "Name", "Description", "Price", "ImageUrl", "Brand", "Category")
SELECT ('10000000-0000-0000-0000-' || lpad(n::text, 12, '0'))::uuid,
    brand || ' ' || model || ' ' || lpad(n::text, 7, '0'),
    description || ' Variant ' || (spec_hash % 64 + 1)::text ||
        '. Synthetic sample SKU ' || n::text || '; specifications are test data.',
    (base_cents + price_hash % span_cents)::numeric / 100,
    '/products/' || icon || '.svg', brand, category
FROM products
ON CONFLICT ("Id") DO NOTHING;
COMMIT;

-- The databases have separate owners/boundaries; interrupted batches are safe to rerun.
\connect inventory_db
BEGIN;
SELECT pg_advisory_xact_lock(141, 1000000);
WITH fixture AS (
    SELECT n, ('x' || substr(md5(n::text || ':stock'), 1, 8))::bit(32)::bigint AS h
    FROM generate_series(:first::integer, :last::integer) n
)
INSERT INTO "Stocks" ("ProductId", "AvailableQuantity", "ReservedQuantity")
SELECT ('10000000-0000-0000-0000-' || lpad(n::text, 12, '0'))::uuid,
    CASE WHEN h % 100 < 5 THEN 0 WHEN h % 100 < 10 THEN 1 + (h / 100 % 4)::integer
         ELSE 10 + (h / 100 % 491)::integer END,
    0
FROM fixture
ON CONFLICT ("ProductId") DO NOTHING;
COMMIT;
