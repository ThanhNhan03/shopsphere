"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useIsMutating, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, money } from "@/lib/api";
import type { CatalogProduct, Product, ProductPage, Stock, StockAvailability } from "@/types";
import { useBasket, useBasketChange, useCustomer } from "@/components/store-provider";
import { ProductImage } from "@/components/product-image";
import { ErrorMessage, Loading } from "@/components/feedback";
import { Icon } from "@/components/icon";
import { deviceIcons } from "@/lib/device-icons";

export function Catalog({
  initialQuery = "",
  initialCategory = "All",
}: {
  initialQuery?: string;
  initialCategory?: string;
}) {
  const router = useRouter();
  const [category, setCategory] = useState(initialCategory);
  const [sort, setSort] = useState("name");
  const [search, setSearch] = useState(initialQuery.trim());
  const [page, setPage] = useState(1);
  const [includeOutOfStock, setIncludeOutOfStock] = useState(false);
  const pageSize = 24;
  const products = useQuery({
    queryKey: ["products", category, search, sort, page, includeOutOfStock],
    queryFn: ({ signal }) => {
      const query = new URLSearchParams({ sort, page: String(page), pageSize: String(pageSize) });
      if (category !== "All") query.set("category", category);
      if (search) query.set("q", search);
      query.set("includeOutOfStock", String(includeOutOfStock));
      return api<ProductPage>(`/api/products?${query}`, { signal });
    },
    refetchInterval: 30000,
  });
  const categoryList = useQuery({
    queryKey: ["categories"],
    queryFn: () => api<string[]>("/api/categories"),
  });
  const spotlight = useQuery({
    queryKey: ["product", "00000000-0000-0000-0000-000000000001"],
    queryFn: () => api<Product>("/api/products/00000000-0000-0000-0000-000000000001"),
  });
  const categories = [
    "All",
    ...(categoryList.data || ["Accessories", "Audio", "Laptops", "Monitors", "Storage"]),
  ];
  const featured = spotlight.data;
  const visible = products.data?.items || [];
  const ids = visible.map(p => p.id);
  const availability = useQuery({
    queryKey: ["stock-page", ids],
    queryFn: ({ signal }) => api<StockAvailability[]>(`/api/inventory/availability?ids=${ids.join(",")}`, { signal }),
    enabled: ids.length > 0,
    refetchInterval: 10000,
  });
  const stockById = new Map(availability.data?.map(s => [s.productId, s.availableQuantity]));
  return (
    <>
      <section className="hero">
        <div className="hero-copy">
          <p className="eyebrow">
            <span className="tiny-dot" /> THE EVERYDAY UPGRADE
          </p>
          <h1>
            Good tech.
            <br />
            Better <span className="slogan-accent">everyday.</span>
          </h1>
          <p className="hero-description">
            For the things you do. And the things you love.
            <br className="desktop-break" /> Discover a considered collection of
            everyday essentials.
          </p>
          <a href="#collection" className="button">
            Find your next essential <Icon name="arrow-up" size={18} />
          </a>
          <div className="hero-footnote">
            <span className="mini-orbits">
              <Icon name="headphones" size={16} />
              <Icon name="box" size={16} />
              <Icon name="spark" size={16} />
            </span>
            <span>Everyday essentials. More possibilities.</span>
          </div>
        </div>
        <div className="hero-showcase">
          <div className="showcase-top">
            <span>THE SPOTLIGHT / 01</span>
            <Icon name="spark" size={22} />
          </div>
          <div className="hero-orbit" aria-hidden="true" />
          <span className="showcase-watermark" aria-hidden="true">
            AIR
          </span>
          <div className="hero-product">
            <ProductImage
              src={deviceIcons.laptop}
              name={featured?.name || "MacBook Air"}
              large
            />
          </div>
          <div className="hero-caption">
            <div>
              <span className="eyebrow">LIGHTWEIGHT. LIMITLESS.</span>
              <h2>{featured?.name || "MacBook Air M4"}</h2>
              <span>
                {featured
                  ? money(featured.price)
                  : "Meet your next everyday essential"}
              </span>
            </div>
            <Link
              href={`/products/${featured?.id || "00000000-0000-0000-0000-000000000001"}`}
              className="circle-link"
              aria-label="Explore MacBook Air"
            >
              <Icon name="arrow-up" size={24} />
            </Link>
          </div>
          <span className="showcase-tag">
            A little lighter.
            <br />A lot more possible.
          </span>
        </div>
      </section>
      <div className="brand-strip">
        <span>
          THE NAMES YOU KNOW.
          <br />
          <strong>THE TECH YOU LOVE.</strong>
        </span>
        {["Apple", "Sony", "Logitech", "Samsung", "Keychron"].map((brand) => (
          <span className="brand-word" key={brand}>
            {brand}
          </span>
        ))}
      </div>
      <section id="collection" className="collection">
        <div className="section-title">
          <div>
            <p className="eyebrow">LESS, BUT BETTER</p>
            <h2>
              Find your kind of essential<span>.</span>
            </h2>
          </div>
          <p>A good day starts with good tools.</p>
        </div>
        <div className="collection-toolbar">
          <div className="filters" aria-label="Product categories">
            {categories.map((c) => (
              <button
                key={c}
                className={c === category ? "selected" : ""}
                onClick={() => { setCategory(c); setPage(1); }}
                aria-pressed={c === category}
              >
                {c === "All" ? "All essentials" : c}
              </button>
            ))}
          </div>
          <div className="catalog-options">
          <label className="availability-filter">
            <input type="checkbox" checked={includeOutOfStock}
              onChange={e => { setIncludeOutOfStock(e.target.checked); setPage(1); }} />
            Include out of stock
          </label>
          <label className="sort-control">
            <span className="sr-only">Sort products</span>
            <select value={sort} onChange={(e) => { setSort(e.target.value); setPage(1); }}>
              <option value="name">Name: A–Z</option>
              <option value="price-low">Price: low to high</option>
              <option value="price-high">Price: high to low</option>
            </select>
          </label>
          </div>
        </div>
        {search && (
          <div className="search-result">
            Results for <strong>“{search}”</strong>
            <button
              className="text-button"
              onClick={() => {
                setSearch("");
                setPage(1);
                router.replace(
                  category === "All"
                    ? "/#collection"
                    : `/?category=${encodeURIComponent(category)}#collection`,
                  { scroll: false },
                );
              }}
            >
              Clear search <Icon name="close" size={14} />
            </button>
          </div>
        )}
        <ErrorMessage error={products.error} />
        {products.isPending ? (
          <Loading />
        ) : !products.error && !visible.length ? (
          <div className="empty">
            <Icon name="search" size={36} />
            <h2>No essentials found.</h2>
            <p>Try another product name or category.</p>
            <button
              className="button"
              onClick={() => {
                setSearch("");
                setCategory("All");
                setIncludeOutOfStock(false);
                setPage(1);
                router.replace("/#collection", { scroll: false });
              }}
            >
              View all essentials <Icon name="arrow" />
            </button>
          </div>
        ) : (
          <div className="product-grid">
            {visible.map((p) => (
              <ProductCard key={p.id} product={p}
                available={availability.error ? undefined : stockById.get(p.id)}
                stockError={!!availability.error} />
            ))}
          </div>
        )}
        {!!products.data?.totalPages && products.data.totalPages > 1 && (
          <nav className="catalog-pagination" aria-label="Product pages">
            <button className="button" disabled={page === 1 || products.isFetching}
              onClick={() => setPage((current) => current - 1)}>Previous</button>
            <span role="status" aria-live="polite">
              Page {page.toLocaleString()} of {products.data.totalPages.toLocaleString()}
            </span>
            <button className="button" disabled={page >= products.data.totalPages || products.isFetching}
              onClick={() => setPage((current) => current + 1)}>Next</button>
          </nav>
        )}
        <div className="collection-end">
          <span>Thoughtfully chosen. Ready for your everyday.</span>
          <span role="status" aria-live="polite">
            {products.data?.totalCount.toLocaleString() || 0} {includeOutOfStock ? "essentials" : "available essentials"} <Icon name="spark" size={16} />
          </span>
        </div>
      </section>
      <section className="editorial-banner">
        <div>
          <p className="eyebrow">TUNE INTO YOUR WORLD</p>
          <h2>
            Less noise.
            <br />
            More <span className="slogan-accent">you.</span>
          </h2>
          <p>Make a little room for your favorite sound.</p>
          <Link href="/?category=Audio#collection" className="button light">
            Explore audio <Icon name="arrow-up" size={18} />
          </Link>
        </div>
        <div className="editorial-art">
          <span aria-hidden="true">SOUND</span>
          <ProductImage
            src={deviceIcons.headphones}
            name="Headphones"
            large
          />
        </div>
        <span className="editorial-side">EVERYDAY / ESSENTIALS</span>
      </section>
      <section className="benefits" aria-label="Shopping experience">
        <div>
          <Icon name="box" size={28} />
          <div>
            <h3>A considered collection</h3>
            <p>Everyday tools from familiar brands.</p>
          </div>
        </div>
        <div>
          <Icon name="shield" size={28} />
          <div>
            <h3>Your account. Your essentials.</h3>
            <p>Your bag stays with your Google account.</p>
          </div>
        </div>
        <div>
          <Icon name="spark" size={28} />
          <div>
            <h3>Simple at every step</h3>
            <p>From finding your favorite to checking out.</p>
          </div>
        </div>
      </section>
    </>
  );
}
function ProductCard({ product: p, available, stockError }: { product: CatalogProduct; available?: number; stockError: boolean }) {
  const change = useBasketChange();
  const basket = useBasket();
  const customer = useCustomer();
  const writes = useIsMutating({ mutationKey: ["basket-change", customer] });
  const client = useQueryClient();
  const inBag = basket.data?.items.find(i => i.productId === p.id)?.quantity ?? 0;
  const limitReached = available !== undefined && available > 0 && inBag >= Math.min(99, available);
  const unavailable = available === 0;
  const checking = available === undefined;
  const basketUnknown = !!customer && (!basket.data || !!basket.error);
  return (
    <article className={`product-card product-${p.category.toLowerCase()}`}>
      <Link href={`/products/${p.id}`} className="product-visual">
        <span className="product-category">{p.category}</span>
        <span className="product-view">
          <Icon name="arrow-up" size={18} />
        </span>
        <ProductImage src={p.imageUrl} name={p.name} category={p.category} />
      </Link>
      <div className="product-meta">
        <p>{p.brand}</p>
        <Link href={`/products/${p.id}`}>
          <h3>{p.name}</h3>
        </Link>
        <div className="product-bottom">
          <strong>{money(p.price)}</strong>
          <button
            className="add-button"
            aria-label={unavailable ? `${p.name} is out of stock` : `Add ${p.name} to bag`}
            disabled={writes > 0 || unavailable || checking || limitReached || basketUnknown}
            onClick={() =>
              change.mutate({ productId: p.id, quantity: 1, method: "POST" }, {
                onError: () => {
                  client.invalidateQueries({ queryKey: ["stock-page"] });
                  client.invalidateQueries({ queryKey: ["products"] });
                },
              })
            }
          >
            <Icon name={change.isSuccess ? "check" : "plus"} size={18} />
            <span>{unavailable ? "Out of stock" : checking ? stockError ? "Unavailable" : "Checking…"
              : limitReached ? "Limit in bag" : change.isPending ? "Adding…" : "Add to bag"}</span>
          </button>
        </div>
        <p className={`product-availability${unavailable || stockError ? " unavailable" : ""}`}>
          {checking ? stockError ? "Availability cannot be checked right now." : "Checking availability…"
            : unavailable ? "Currently out of stock." : available <= 5 ? `Only ${available} left in stock.` : "In stock."}
          {limitReached && <> <Link href="/cart">Review your bag</Link></>}
        </p>
        <ErrorMessage error={change.error} />
        {change.isSuccess && (
          <span role="status" className="success">
            Added to your bag
          </span>
        )}
      </div>
    </article>
  );
}
export function ProductDetail({ id }: { id: string }) {
  const product = useQuery({
    queryKey: ["product", id],
    queryFn: () => api<Product>(`/api/products/${id}`),
  });
  const [quantity, setQuantity] = useState(1);
  const basket = useBasket();
  const customer = useCustomer();
  const writes = useIsMutating({ mutationKey: ["basket-change", customer] });
  const stock = useQuery({
    queryKey: ["stock", id],
    queryFn: ({ signal }) => api<Stock>(`/api/inventory/${id}`, { signal }),
    refetchInterval: 5000,
  });
  const available = stock.data?.availableQuantity ?? 0;
  const inBag = basket.data?.items.find(i => i.productId === id)?.quantity ?? 0;
  const remaining = Math.max(0, Math.min(99, available) - inBag);
  const limit = Math.max(1, remaining);
  const selectedQuantity = Math.min(quantity, limit);
  const change = useBasketChange();
  if (product.isPending) return <Loading />;
  if (!product.data) return <ErrorMessage error={product.error} />;
  const p = product.data;
  return (
    <>
      <Link href="/#collection" className="back">
        ← Back to the collection
      </Link>
      <div className="detail">
        <div className={`detail-visual product-${p.category.toLowerCase()}`}>
          <span className="product-category">{p.category}</span>
          <ProductImage
            src={p.imageUrl}
            name={p.name}
            category={p.category}
            large
          />
          <span className="detail-caption">
            THOUGHTFULLY SELECTED / {p.brand.toUpperCase()}
          </span>
        </div>
        <div className="detail-copy">
          <p className="eyebrow">
            {p.brand} / {p.category}
          </p>
          <h1>{p.name}</h1>
          <p className="detail-price">
            {money(p.price)} <span>USD</span>
          </p>
          <p className="description">{p.description}</p>
          <p className="input-hint" role="status">
            {stock.isPending ? "Checking availability…" : stock.error ? "Availability is temporarily unavailable."
              : available === 0 ? "Out of stock." : available <= 5 ? `Only ${available} left in stock.` : "In stock."}
          </p>
          <div className="detail-actions">
            <div>
              <span className="input-caption">Quantity</span>
              <div className="quantity-control">
                <button
                  aria-label="Decrease quantity"
                  disabled={selectedQuantity <= 1 || remaining === 0 || !!stock.error}
                  onClick={() => setQuantity(Math.max(1, selectedQuantity - 1))}
                >
                  <Icon name="minus" size={16} />
                </button>
                <label className="sr-only" htmlFor="product-quantity">
                  Quantity
                </label>
                <input
                  id="product-quantity"
                  type="number"
                  min={1}
                  max={limit}
                  value={selectedQuantity}
                  disabled={remaining === 0 || !!stock.error}
                  onChange={(e) =>
                    setQuantity(
                      Math.max(1, Math.min(limit, Number(e.target.value) || 1)),
                    )
                  }
                />
                <button
                  aria-label="Increase quantity"
                  disabled={selectedQuantity >= limit || remaining === 0 || !!stock.error}
                  onClick={() => setQuantity(selectedQuantity + 1)}
                >
                  <Icon name="plus" size={16} />
                </button>
              </div>
            </div>
            <button
              className="button"
              disabled={writes > 0 || !stock.data || remaining === 0 || !!stock.error || (!!customer && (!basket.data || !!basket.error))}
              onClick={() =>
                change.mutate({ productId: p.id, quantity: selectedQuantity, method: "POST" }, { onError: () => stock.refetch() })
              }
            >
              <Icon name="bag" />
              {available === 0 && !stock.isPending ? "Out of stock" : remaining === 0 && available > 0 ? "Limit in bag"
                : change.isPending ? "Adding to your bag…" : "Add to shopping bag"}
              <Icon name="arrow" size={18} />
            </button>
          </div>
          {available > 0 && remaining === 0 && (
            <p className="input-hint">You already have the maximum quantity in your bag. <Link href="/cart">Review your bag</Link>.</p>
          )}
          <ErrorMessage error={change.error} />
          {change.isSuccess && (
            <p role="status" className="success">
              <Icon name="check" size={16} />
              Added to your bag. <Link href="/cart">View bag →</Link>
            </p>
          )}
          <div className="detail-assurance">
            <Icon name="shield" size={20} />
            <p>
              Your bag is saved to your account.
              <br />
              <span>Demo store · Prices in USD · No shipping or taxes.</span>
            </p>
          </div>
          <details className="product-notes">
            <summary>Before you checkout</summary>
            <p>
              Availability is checked when you update your bag and again before creating an order. Items are
              reserved while payment is pending and released if the order is
              cancelled.
            </p>
          </details>
        </div>
      </div>
    </>
  );
}
