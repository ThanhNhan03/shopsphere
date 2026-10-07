import type { Metadata } from "next";
import Link from "next/link";
import { StoreProvider } from "@/components/store-provider";
import { Header } from "@/components/header";
import "./globals.css";
export const metadata: Metadata = { title: "ShopSphere — Your next upgrade", description: "A curated technology store and distributed checkout demo." };
export default function Layout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body><div className="topbar">A little tech. A lot of possibility. <span>ShopSphere demo store</span></div><StoreProvider><Header /><main>{children}</main></StoreProvider><footer><Link href="/" className="logo">ShopSphere.</Link><p>Thoughtfully selected. Simply yours.</p><span>Demo store · USD · No real fulfillment</span></footer></body></html>;
}
