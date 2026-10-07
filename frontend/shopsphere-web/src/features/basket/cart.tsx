"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
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
import { BagQuantity } from "@/components/bag-quantity";
import { money } from "@/lib/api";
export function Cart() {
  const basket = useBasket();
  const change = useBasketChange();
  const customer = useCustomer();
  const router = useRouter();
  if (!customer) return <SignInPrompt returnUrl="/cart" />;
  if (basket.isPending) return <Loading />;
  if (basket.error && !basket.data) return <ErrorMessage error={basket.error} />;
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
      <ErrorMessage error={basket.error} />
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
              <span>UNIT PRICE / QUANTITY / ITEM TOTAL</span>
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
                  {item.availableQuantity <= 5 || item.quantity > item.availableQuantity ? (
                    <p className={`cart-stock-status${item.quantity > item.availableQuantity ? " unavailable" : ""}`}>
                      {item.availableQuantity === 0 ? "Out of stock. Remove this item to continue."
                        : item.quantity > item.availableQuantity ? `Only ${item.availableQuantity} available. Reduce your quantity.`
                        : `Only ${item.availableQuantity} left in stock.`}
                    </p>
                  ) : null}
                  <button
                    className="text-button bag-action"
                    disabled={change.isPending}
                    data-pending={change.isPending || undefined}
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
                  <div className="cart-unit-price">
                    <span className="quantity-caption">Unit price</span>
                    <strong>{money(item.unitPrice)}</strong>
                  </div>
                  <BagQuantity
                    quantity={
                      change.isPending && change.variables?.productId === item.productId
                        && change.variables.method === "PUT"
                        ? change.variables.quantity ?? item.quantity : item.quantity
                    }
                    productName={item.name}
                    disabled={change.isPending || item.availableQuantity === 0}
                    busy={change.isPending && change.variables?.productId === item.productId}
                    maximum={item.availableQuantity}
                    onChange={(quantity) =>
                      change.mutate({
                        productId: item.productId,
                        quantity,
                        method: "PUT",
                      })
                    }
                  />
                  <div className="cart-line-total">
                    <span className="quantity-caption">Item total</span>
                    <strong>{money(item.unitPrice * item.quantity)}</strong>
                  </div>
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
            {!basket.data.canCheckout && (
              <p className="field-error" role="status">Adjust or remove unavailable items before checking out.</p>
            )}
            <button type="button" className="button full bag-action"
              disabled={change.isPending || !basket.data.canCheckout || !!basket.error}
              data-pending={(change.isPending && basket.data.canCheckout && !basket.error) || undefined}
              onClick={() => router.push("/checkout")}>
              Continue to checkout <Icon name="arrow" />
            </button>
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
