"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { z } from "zod";
import { useBasket, useCustomer } from "@/components/store-provider";
import { ErrorMessage, Loading } from "@/components/feedback";
import { api, json, money } from "@/lib/api";
import type { Order } from "@/types";
const schema = z.object({ customerName: z.string().trim().min(1, "Enter your name.").max(100), email: z.email("Enter a valid email address.").max(254) });
export function Checkout() {
  const basket = useBasket(); const customer = useCustomer(); const router = useRouter();
  const [validation, setValidation] = useState<Error | null>(null);
  const order = useMutation({ mutationFn: (data: z.infer<typeof schema>) => {
    let checkoutId = sessionStorage.getItem("shopsphere-checkout");
    if (!checkoutId) { checkoutId = crypto.randomUUID(); sessionStorage.setItem("shopsphere-checkout", checkoutId); }
    return api<Order>("/api/orders", json("POST", { ...data, customerId: customer, checkoutId }));
  }, onSuccess: result => { sessionStorage.removeItem("shopsphere-checkout"); router.push(`/orders/${result.id}`); } });
  if (basket.isPending) return <Loading />;
  if (basket.error) return <ErrorMessage error={basket.error} />;
  if (!basket.data?.items.length) return <div className="empty"><h1>Your bag is empty.</h1><Link href="/">Browse the collection →</Link></div>;
  return <><Link href="/cart" className="back">← Back to bag</Link><p className="eyebrow">ONE STEP CLOSER</p><h1>Make it yours.</h1><div className="checkout-grid"><form className="checkout-form" onSubmit={event => {
    event.preventDefault(); const result = schema.safeParse(Object.fromEntries(new FormData(event.currentTarget)));
    if (!result.success) { setValidation(new Error(result.error.issues[0].message)); return; }
    setValidation(null); order.mutate(result.data);
  }}><h2>Your details</h2><p>We&apos;ll use these details for your demo order.</p><label>Full name<input name="customerName" autoComplete="name" required maxLength={100} placeholder="Your name" /></label><label>Email address<input name="email" type="email" autoComplete="email" required maxLength={254} placeholder="you@example.com" /></label><ErrorMessage error={validation || order.error} /><button className="button" disabled={order.isPending || !customer}>{order.isPending ? "Creating your order…" : "Create order & continue →"}</button><p className="fineprint">Stock is reserved before payment. Your order page will show the next step.</p></form>
    <aside className="summary"><h2>Your order</h2>{basket.data.items.map(i => <div className="summary-line" key={i.productId}><span>{i.name} × {i.quantity}</span><strong>{money(i.unitPrice * i.quantity)}</strong></div>)}<div className="summary-line total"><span>Total</span><strong>{money(basket.data.total)}</strong></div></aside></div></>;
}
