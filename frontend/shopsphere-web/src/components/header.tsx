"use client";
import Link from "next/link";
import { useBasket } from "./store-provider";
export function Header() {
  const { data } = useBasket();
  const count = data?.items.reduce((sum, item) => sum + item.quantity, 0) || 0;
  return <header className="header"><Link href="/" className="logo"><span className="logo-mark">S</span>ShopSphere<span className="logo-dot">.</span></Link>
    <nav aria-label="Main navigation"><Link href="/">Shop all</Link><Link href="/cart" className="cart-link">Bag <span aria-label={`${count} items`}>{count}</span></Link></nav></header>;
}
