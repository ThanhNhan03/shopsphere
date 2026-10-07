import type { Metadata } from "next";
import Link from "next/link";
import { StoreProvider } from "@/components/store-provider";
import { Header } from "@/components/header";
import { Brand } from "@/components/brand";
import { Icon } from "@/components/icon";
import { StoreChrome } from "@/components/store-chrome";
import "./globals.css";
export const metadata: Metadata = {
  title: "ShopSphere — Good tech. Better everyday.",
  description:
    "A considered collection of everyday technology. Find your next essential at ShopSphere.",
};
export default function Layout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>
        <a href="#main-content" className="skip-link">
          Skip to content
        </a>
        <StoreChrome><div className="topbar">
          <span>A considered collection. A better everyday.</span>
          <span className="topbar-note">
            Local demo store <span className="tiny-dot" /> USD
          </span>
        </div></StoreChrome>
        <StoreProvider>
          <StoreChrome><Header /></StoreChrome>
          <main id="main-content">{children}</main>
        </StoreProvider>
        <StoreChrome><footer>
          <div className="footer-top">
            <div>
              <Brand />
              <p>
                Less scrolling. More discovering.
                <br />
                Good technology, thoughtfully selected.
              </p>
            </div>
            <div className="footer-nav">
              <span className="eyebrow">EXPLORE</span>
              <Link href="/#collection">The collection</Link>
              <Link href="/cart">Your shopping bag</Link>
              <Link href="/login">Your account</Link>
            </div>
            <div className="footer-note">
              <Icon name="spark" size={28} />
              <p>
                A small collection.
                <br />A world of possibilities.
              </p>
            </div>
          </div>
          <div className="footer-bottom">
            <span>© {new Date().getFullYear()} ShopSphere</span>
            <span>Demo purchases · No real fulfillment</span>
            <a href="#main-content">
              Back to top <Icon name="arrow-up" size={14} />
            </a>
          </div>
        </footer></StoreChrome>
      </body>
    </html>
  );
}
