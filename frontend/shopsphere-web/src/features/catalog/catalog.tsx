"use client";
import Link from "next/link";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { api, money } from "@/lib/api";
import type { Product } from "@/types";
import { useBasketChange, useCustomer } from "@/components/store-provider";
import { ProductImage } from "@/components/product-image";
import { ErrorMessage, Loading } from "@/components/feedback";

export function Catalog() {
  const [category, setCategory] = useState("All");
  const products = useQuery({ queryKey: ["products"], queryFn: () => api<Product[]>("/api/products") });
  const categories = ["All", ...new Set(products.data?.map(p => p.category) || [])];
  return <><section className="hero"><div><p className="eyebrow">GOOD TECH. GREAT POSSIBILITIES.</p><h1>Your next upgrade<br />starts here<span>.</span></h1>
    <p>Thoughtfully selected technology for the way you work,<br className="desktop-break" /> create, and unwind. Find something you&apos;ll love.</p>
    <a href="#collection" className="button">Explore the collection <span>↗</span></a><div className="hero-note"><span>✦</span> A small collection. A lot of possibilities.</div></div>
    <div className="hero-art"><span className="art-caption">THE EVERYDAY ESSENTIAL</span><ProductImage src="/products/laptop.svg" name="MacBook Air" large /><div className="art-detail"><div>MacBook Air M4<small>Light on your desk. Big on performance.</small></div><span>↗</span></div></div></section>
    <section id="collection" className="collection"><div className="section-title"><div><p className="eyebrow">CURATED FOR YOU</p><h2>Find your everyday essential.</h2></div><span>{products.data?.length || 0} products</span></div>
      <div className="filters" aria-label="Product categories">{categories.map(c => <button key={c} className={c === category ? "selected" : ""} onClick={() => setCategory(c)} aria-pressed={c === category}>{c}</button>)}</div>
      <ErrorMessage error={products.error} />{products.isPending ? <Loading /> : <div className="product-grid">{products.data?.filter(p => category === "All" || p.category === category).map(p => <ProductCard key={p.id} product={p} />)}</div>}
    </section><div className="benefits"><div><span>01</span><strong>Tech you can count on</strong><p>Everyday essentials from familiar brands.</p></div><div><span>02</span><strong>Simple from start to finish</strong><p>Your basket, checkout, and order in one place.</p></div><div><span>03</span><strong>Made for exploring</strong><p>A demo store built around a real purchase flow.</p></div></div></>;
}
function ProductCard({ product: p }: { product: Product }) {
  const change = useBasketChange(); const customer = useCustomer();
  return <article className="product-card"><Link href={`/products/${p.id}`} className="product-visual"><span className="product-category">{p.category}</span><ProductImage src={p.imageUrl} name={p.name} /></Link>
    <div className="product-meta"><p>{p.brand}</p><Link href={`/products/${p.id}`}><h3>{p.name}</h3></Link><div className="product-bottom"><strong>{money(p.price)}</strong><button className="add-button" aria-label={`Add ${p.name} to bag`} disabled={!customer || change.isPending} onClick={() => change.mutate({ productId: p.id, quantity: 1, method: "POST" })}>{change.isPending ? "…" : "+"}</button></div>
      <ErrorMessage error={change.error} />{change.isSuccess && <span role="status" className="success">Added to bag</span>}</div></article>;
}
export function ProductDetail({ id }: { id: string }) {
  const product = useQuery({ queryKey: ["product", id], queryFn: () => api<Product>(`/api/products/${id}`) });
  const [quantity, setQuantity] = useState(1); const change = useBasketChange(); const customer = useCustomer();
  if (product.isPending) return <Loading />;
  if (!product.data) return <ErrorMessage error={product.error} />;
  const p = product.data;
  return <><Link href="/" className="back">← Back to collection</Link><div className="detail"><div className="detail-visual"><ProductImage src={p.imageUrl} name={p.name} large /></div><div><p className="eyebrow">{p.brand} / {p.category}</p><h1>{p.name}</h1><p className="detail-price">{money(p.price)}</p><p className="description">{p.description}</p>
    <label className="quantity-label">Quantity<input type="number" min={1} max={99} value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>
    <button className="button" disabled={!customer || change.isPending} onClick={() => change.mutate({ productId: p.id, quantity, method: "POST" })}>{change.isPending ? "Adding…" : "Add to bag"}</button><ErrorMessage error={change.error} />{change.isSuccess && <p role="status" className="success">Added to bag. <Link href="/cart">View your bag →</Link></p>}<p className="fineprint">Demo catalog • All prices in USD</p></div></div></>;
}
