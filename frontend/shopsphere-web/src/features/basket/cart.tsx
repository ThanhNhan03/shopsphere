"use client";
import Link from "next/link";
import { SignInPrompt } from "@/components/sign-in-prompt";
import {
  useBasket,
  useBasketChange,
  useCustomer,
} from "@/components/store-provider";
import { ProductImage } from "@/components/product-image";
import { ErrorMessage, Loading } from "@/components/feedback";
import { CheckoutSteps } from "@/components/checkout-steps";
import { Icon } from "@/components/icon";
import { money } from "@/lib/api";
export function Cart() {
  const basket = useBasket();
  const change = useBasketChange();
  const customer = useCustomer();
  if (!customer) return <SignInPrompt returnUrl="/cart" />;
  if (basket.isPending) return <Loading />;
  if (basket.error) return <ErrorMessage error={basket.error} />;
  const count = basket.data?.items.reduce((s, i) => s + i.quantity, 0) || 0;
  return (
    <>
      <CheckoutSteps active={0} />
      <div className="page-heading">
        <div>
          <p className="eyebrow">YOUR EVERYDAY, UPGRADED</p>
          <h1>
            Your shopping bag<span>.</span>
          </h1>
          <p>
            {count
              ? `${count} ${count === 1 ? "essential" : "essentials"}, chosen by you.`
              : "Make room for something good."}
          </p>
        </div>
        <Link href="/#collection" className="back">
          Keep exploring <Icon name="arrow-up" size={17} />
        </Link>
      </div>
      <ErrorMessage error={change.error} />
      {!basket.data?.items.length ? (
        <div className="empty">
          <span className="empty-icon">
            <Icon name="bag" size={32} />
          </span>
          <h2>Your next essential is out there.</h2>
          <p>Find something you love, and make it part of your everyday.</p>
          <Link href="/#collection" className="button">
            Explore the collection <Icon name="arrow" />
          </Link>
        </div>
      ) : (
        <div className="checkout-grid">
          <div className="bag-items">
            <div className="bag-table-head">
              <span>PRODUCT</span>
              <span>QUANTITY / TOTAL</span>
            </div>
            {basket.data.items.map((item) => (
              <div className="cart-row" key={item.productId}>
                <Link
                  href={`/products/${item.productId}`}
                  className="cart-image"
                >
                  <ProductImage src={item.imageUrl} name={item.name} />
                </Link>
                <div className="cart-info">
                  <Link href={`/products/${item.productId}`}>
                    <h3>{item.name}</h3>
                  </Link>
                  <p>{money(item.unitPrice)} each</p>
                  <button
                    className="text-button"
                    disabled={change.isPending}
                    onClick={() =>
                      change.mutate({
                        productId: item.productId,
                        method: "DELETE",
                      })
                    }
                  >
                    Remove
                  </button>
                </div>
                <div className="cart-quantity">
                  <label className="sr-only" htmlFor={item.productId}>
                    Quantity for {item.name}
                  </label>
                  <select
                    id={item.productId}
                    value={item.quantity}
                    disabled={change.isPending}
                    onChange={(e) =>
                      change.mutate({
                        productId: item.productId,
                        quantity: Number(e.target.value),
                        method: "PUT",
                      })
                    }
                  >
                    {Array.from({ length: 99 }, (_, i) => (
                      <option key={i + 1}>{i + 1}</option>
                    ))}
                  </select>
                  <strong>{money(item.unitPrice * item.quantity)}</strong>
                </div>
              </div>
            ))}
            <div className="bag-note">
              <Icon name="shield" />
              <span>Your bag stays saved to your account. Take your time.</span>
            </div>
          </div>
          <aside className="summary">
            <span className="eyebrow">THE GOOD PART</span>
            <h2>Order summary</h2>
            <div className="summary-line">
              <span>Subtotal ({count} items)</span>
              <span>{money(basket.data.total)}</span>
            </div>
            <div className="summary-line">
              <span>Shipping & tax</span>
              <span>Not applied</span>
            </div>
            <div className="summary-line total">
              <span>
                Total <small>USD</small>
              </span>
              <strong>{money(basket.data.total)}</strong>
            </div>
            <Link href="/checkout" className="button full">
              Continue to checkout <Icon name="arrow" />
            </Link>
            <p className="summary-footnote">
              <Icon name="shield" size={16} />
              Google account protected
            </p>
            <p className="fineprint">
              This is a demo purchase. No physical items will be shipped.
            </p>
          </aside>
        </div>
      )}
    </>
  );
}
