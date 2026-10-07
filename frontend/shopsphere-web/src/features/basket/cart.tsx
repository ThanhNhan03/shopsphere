"use client";
import Link from "next/link";
import { useBasket, useBasketChange } from "@/components/store-provider";
import { ProductImage } from "@/components/product-image";
import { ErrorMessage, Loading } from "@/components/feedback";
import { money } from "@/lib/api";
export function Cart() {
  const basket = useBasket(); const change = useBasketChange();
  if (basket.isPending) return <Loading />;
  return <><p className="eyebrow">A FEW GOOD CHOICES</p><h1>Your shopping bag.</h1><ErrorMessage error={basket.error || change.error} />
    {!basket.data?.items.length ? <div className="empty"><h2>Your next upgrade is waiting.</h2><p>Add an essential to your bag to get started.</p><Link href="/" className="button">Explore products ↗</Link></div> :
      <div className="checkout-grid"><div>{basket.data.items.map(item => <div className="cart-row" key={item.productId}><Link href={`/products/${item.productId}`} className="cart-image"><ProductImage src={item.imageUrl} name={item.name} /></Link><div className="cart-info"><Link href={`/products/${item.productId}`}><h3>{item.name}</h3></Link><p>{money(item.unitPrice)} each</p><button className="text-button" disabled={change.isPending} onClick={() => change.mutate({ productId: item.productId, method: "DELETE" })}>Remove</button></div>
        <label className="sr-only" htmlFor={item.productId}>Quantity for {item.name}</label><select id={item.productId} value={item.quantity} disabled={change.isPending} onChange={e => change.mutate({ productId: item.productId, quantity: Number(e.target.value), method: "PUT" })}>{Array.from({ length: 99 }, (_, i) => <option key={i + 1}>{i + 1}</option>)}</select><strong>{money(item.unitPrice * item.quantity)}</strong></div>)}</div>
        <aside className="summary"><h2>Order summary</h2><div className="summary-line"><span>Subtotal</span><span>{money(basket.data.total)}</span></div><p className="fineprint">Demo prices. No shipping fees or taxes are added.</p><div className="summary-line total"><span>Total</span><strong>{money(basket.data.total)}</strong></div><Link href="/checkout" className="button full">Continue to checkout →</Link><Link href="/" className="continue">Keep exploring</Link></aside></div>}</>;
}
