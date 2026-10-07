"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, money } from "@/lib/api";
import type { Product } from "@/types";
import { useBasketChange } from "@/components/store-provider";
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
  const [sort, setSort] = useState("featured");
  const [search, setSearch] = useState(initialQuery.trim());
  const products = useQuery({
    queryKey: ["products"],
    queryFn: () => api<Product[]>("/api/products"),
  });
  const categories = [
    "All",
    ...new Set(products.data?.map((p) => p.category) || []),
  ];
  const featured = products.data?.find((p) => p.category === "Laptops");
  const visible =
    products.data
      ?.filter(
        (p) =>
          (category === "All" || p.category === category) &&
          `${p.name} ${p.brand} ${p.category}`
            .toLowerCase()
            .includes(search.toLowerCase()),
      )
      .sort((a, b) =>
        sort === "price-low"
          ? a.price - b.price
          : sort === "price-high"
            ? b.price - a.price
            : a.name.localeCompare(b.name),
      ) || [];
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
            <span>Small collection. Big possibilities.</span>
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
        {[
          ...new Set(
            products.data?.map((p) => p.brand) || [
              "Apple",
              "Sony",
              "Logitech",
              "Samsung",
              "Keychron",
            ],
          ),
        ].map((brand) => (
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
                onClick={() => setCategory(c)}
                aria-pressed={c === category}
              >
                {c === "All" ? "All essentials" : c}
                <span>
                  {c === "All"
                    ? products.data?.length || 0
                    : products.data?.filter((p) => p.category === c).length}
                </span>
              </button>
            ))}
          </div>
          <label className="sort-control">
            <span className="sr-only">Sort products</span>
            <select value={sort} onChange={(e) => setSort(e.target.value)}>
              <option value="featured">Name: A–Z</option>
              <option value="price-low">Price: low to high</option>
              <option value="price-high">Price: high to low</option>
            </select>
          </label>
        </div>
        {search && (
          <div className="search-result">
            Results for <strong>“{search}”</strong>
            <button
              className="text-button"
              onClick={() => {
                setSearch("");
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
                router.replace("/#collection", { scroll: false });
              }}
            >
              View all essentials <Icon name="arrow" />
            </button>
          </div>
        ) : (
          <div className="product-grid">
            {visible.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        )}
        <div className="collection-end">
          <span>Thoughtfully chosen. Ready for your everyday.</span>
          <span role="status" aria-live="polite">
            {visible.length} essentials <Icon name="spark" size={16} />
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
function ProductCard({ product: p }: { product: Product }) {
  const change = useBasketChange();
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
            aria-label={`Add ${p.name} to bag`}
            disabled={change.isPending}
            onClick={() =>
              change.mutate({ productId: p.id, quantity: 1, method: "POST" })
            }
          >
            <Icon name={change.isSuccess ? "check" : "plus"} size={18} />
            <span>{change.isPending ? "Adding…" : "Add to bag"}</span>
          </button>
        </div>
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
          <div className="detail-actions">
            <div>
              <span className="input-caption">Quantity</span>
              <div className="quantity-control">
                <button
                  aria-label="Decrease quantity"
                  disabled={quantity <= 1}
                  onClick={() => setQuantity((q) => q - 1)}
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
                  max={99}
                  value={quantity}
                  onChange={(e) =>
                    setQuantity(
                      Math.max(1, Math.min(99, Number(e.target.value) || 1)),
                    )
                  }
                />
                <button
                  aria-label="Increase quantity"
                  disabled={quantity >= 99}
                  onClick={() => setQuantity((q) => q + 1)}
                >
                  <Icon name="plus" size={16} />
                </button>
              </div>
            </div>
            <button
              className="button"
              disabled={change.isPending}
              onClick={() =>
                change.mutate({ productId: p.id, quantity, method: "POST" })
              }
            >
              <Icon name="bag" />
              {change.isPending ? "Adding to your bag…" : "Add to shopping bag"}
              <Icon name="arrow" size={18} />
            </button>
          </div>
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
              Availability is checked when you create an order. Items are
              reserved while payment is pending and released if the order is
              cancelled.
            </p>
          </details>
        </div>
      </div>
    </>
  );
}
