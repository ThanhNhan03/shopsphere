"use client";
import Link from "next/link";
import { SignInPrompt } from "@/components/sign-in-prompt";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { z } from "zod";
import {
  useBasket,
  useCustomer,
  useSession,
} from "@/components/store-provider";
import { ErrorMessage, Loading } from "@/components/feedback";
import { CheckoutSteps } from "@/components/checkout-steps";
import { Icon } from "@/components/icon";
import { ProductImage } from "@/components/product-image";
import { api, json, money } from "@/lib/api";
import type { Order } from "@/types";
const schema = z.object({
  customerName: z
    .string()
    .trim()
    .min(1, "Please enter your full name.")
    .max(100, "Use 100 characters or fewer."),
  email: z.email("Please enter a valid email address.").max(254),
});
type Fields = z.infer<typeof schema>;
export function Checkout() {
  const basket = useBasket();
  const customer = useCustomer();
  const router = useRouter();
  const session = useSession();
  const [errors, setErrors] = useState<Partial<Record<keyof Fields, string>>>(
    {},
  );
  const order = useMutation({
    mutationFn: (data: Fields) => {
      const key = `shopsphere-checkout-${customer}`;
      let checkoutId = sessionStorage.getItem(key);
      if (!checkoutId) {
        checkoutId = crypto.randomUUID();
        sessionStorage.setItem(key, checkoutId);
      }
      return api<Order>(
        "/api/orders",
        json("POST", { ...data, customerId: customer, checkoutId }),
      );
    },
    onSuccess: (result) => {
      sessionStorage.removeItem(`shopsphere-checkout-${customer}`);
      router.push(`/orders/${result.id}`);
    },
  });
  if (!customer) return <SignInPrompt returnUrl="/checkout" />;
  if (basket.isPending) return <Loading />;
  if (basket.error) return <ErrorMessage error={basket.error} />;
  if (basket.data?.items.some(i => !i.isAvailable)) return <div className="empty"><h1>Some products are unavailable.</h1><p>Remove them from your bag before checking out.</p><Link href="/cart" className="button">Review your bag</Link></div>;
  if (!basket.data?.items.length)
    return (
      <div className="empty">
        <span className="empty-icon">
          <Icon name="bag" size={32} />
        </span>
        <h1>Your bag is empty.</h1>
        <p>Find your everyday essential before checking out.</p>
        <Link href="/#collection" className="button">
          Explore the collection <Icon name="arrow" />
        </Link>
      </div>
    );
  function validateField(name: keyof Fields, value: string) {
    const result = schema.shape[name].safeParse(value);
    setErrors((e) => ({
      ...e,
      [name]: result.success ? undefined : result.error.issues[0].message,
    }));
  }
  return (
    <>
      <CheckoutSteps active={1} />
      <div className="page-heading">
        <div>
          <p className="eyebrow">A LITTLE CLOSER</p>
          <h1>
            Make it yours<span>.</span>
          </h1>
          <p>A few details, then you’re on your way.</p>
        </div>
        <Link href="/cart" className="back">
          ← Back to your bag
        </Link>
      </div>
      <div className="checkout-grid">
        <form
          className="checkout-form"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            const form = event.currentTarget;
            const result = schema.safeParse(
              Object.fromEntries(new FormData(form)),
            );
            if (!result.success) {
              const next: Partial<Record<keyof Fields, string>> = {};
              for (const issue of result.error.issues) {
                const key = issue.path[0] as keyof Fields;
                next[key] ||= issue.message;
              }
              setErrors(next);
              (
                form.elements.namedItem(
                  result.error.issues[0].path[0] as string,
                ) as HTMLElement
              )?.focus();
              return;
            }
            setErrors({});
            order.mutate(result.data);
          }}
        >
          <div className="form-heading">
            <span className="step-number">01</span>
            <div>
              <h2>Your details</h2>
              <p>Pre-filled from your account. Edit if needed.</p>
            </div>
          </div>
          <label htmlFor="customer-name">
            Full name <span>Required</span>
          </label>
          <input
            id="customer-name"
            name="customerName"
            defaultValue={session.data?.user?.name || ""}
            autoComplete="name"
            required
            maxLength={100}
            placeholder="Your full name"
            aria-invalid={!!errors.customerName}
            aria-describedby={errors.customerName ? "name-error" : undefined}
            onBlur={(e) => validateField("customerName", e.target.value)}
          />
          {errors.customerName && (
            <p id="name-error" className="field-error">
              {errors.customerName}
            </p>
          )}
          <label htmlFor="customer-email">
            Email address <span>Required</span>
          </label>
          <input
            id="customer-email"
            name="email"
            defaultValue={session.data?.user?.email || ""}
            type="email"
            autoComplete="email"
            required
            maxLength={254}
            placeholder="you@example.com"
            aria-invalid={!!errors.email}
            aria-describedby={errors.email ? "email-error" : "email-hint"}
            onBlur={(e) => validateField("email", e.target.value)}
          />
          {errors.email ? (
            <p id="email-error" className="field-error">
              {errors.email}
            </p>
          ) : (
            <p id="email-hint" className="input-hint">
              We’ll attach this email to your order.
            </p>
          )}
          <div className="checkout-explainer">
            <Icon name="shield" size={22} />
            <div>
              <strong>What happens next?</strong>
              <p>
                We check availability and reserve your items. Choose your
                payment option on the next page.
              </p>
            </div>
          </div>
          <ErrorMessage error={order.error} />
          <button className="button full" disabled={order.isPending}>
            {order.isPending
              ? "Creating your order…"
              : "Create order & continue"}
            <Icon name="arrow" />
          </button>
          <p className="fineprint">
            No shipping address needed for this demo. Your total is calculated
            by the store.
          </p>
        </form>
        <aside className="summary">
          <p className="eyebrow">YOUR PICKS</p>
          <h2>A few good choices.</h2>
          {basket.data.items.map((i) => (
            <div className="checkout-item" key={i.productId}>
              <div className="checkout-item-image">
                <ProductImage src={i.imageUrl} name={i.name} />
              </div>
              <div>
                <strong>{i.name}</strong>
                <span>Quantity {i.quantity}</span>
              </div>
              <strong>{money(i.unitPrice * i.quantity)}</strong>
            </div>
          ))}
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
          <p className="summary-footnote">
            <Icon name="shield" size={16} />
            Your account. Your order.
          </p>
        </aside>
      </div>
    </>
  );
}
