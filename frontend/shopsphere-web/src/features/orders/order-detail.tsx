"use client";
import Link from "next/link";
import { useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, json, money } from "@/lib/api";
import { ErrorMessage, Loading } from "@/components/feedback";
import type { Order, Payment } from "@/types";
export function OrderDetail({ id }: { id: string }) {
  const client = useQueryClient();
  const order = useQuery({ queryKey: ["order", id], queryFn: () => api<Order>(`/api/orders/${id}`), refetchInterval: q => ["Confirmed", "Cancelled"].includes(q.state.data?.status || "") ? false : 2000 });
  const payment = useQuery({ queryKey: ["payment", id], queryFn: async () => {
    const response = await fetch(`/api/payments/${id}`);
    if (response.status === 404) return null;
    if (!response.ok) throw new Error("Could not load payment details.");
    return response.json() as Promise<Payment>;
  }, enabled: !!order.data && order.data.status !== "Cancelled", refetchInterval: q => ["Completed", "Failed"].includes(q.state.data?.status || "") ? false : 2000 });
  const start = useMutation({ mutationFn: () => api<Payment>("/api/payments/checkout-session", json("POST", { orderId: id })), onSuccess: p => {
    if (p.mode === "Stripe" && p.checkoutUrl) window.location.assign(p.checkoutUrl);
    else client.invalidateQueries({ queryKey: ["payment", id] });
  } });
  const simulate = useMutation({ mutationFn: (paid: boolean) => api<Payment>(`/api/payments/${id}/simulate`, json("POST", { paid })), onSuccess: () => {
    client.invalidateQueries({ queryKey: ["payment", id] }); client.invalidateQueries({ queryKey: ["order", id] });
  } });
  useEffect(() => {
    if (order.data?.status === "Confirmed") client.invalidateQueries({ queryKey: ["basket"] });
  }, [order.data?.status, client]);
  if (order.isPending) return <Loading />;
  if (!order.data) return <ErrorMessage error={order.error} />;
  const o = order.data; const finished = ["Confirmed", "Cancelled"].includes(o.status);
  return <><p className="eyebrow">YOUR ORDER</p><div className="order-heading"><h1>{o.status === "Confirmed" ? "A great choice." : o.status === "Cancelled" ? "Order cancelled." : "Almost yours."}</h1><span className={`status ${o.status.toLowerCase()}`}>{o.status.replace(/([a-z])([A-Z])/g, "$1 $2")}</span></div><p className="order-reference">{o.id}</p>
    <div className="checkout-grid"><div><div className="order-message" role="status">{o.status === "Confirmed" ? "Payment received. Your demo order is confirmed." : o.status === "Cancelled" ? o.cancellationReason : payment.data ? "Your items are reserved. Complete payment to confirm your order." : "We’re checking availability and reserving your items…"}</div>
      <h2>Order details</h2>{o.items.map(i => <div className="summary-line" key={i.productId}><span>{i.name} × {i.quantity}</span><strong>{money(i.unitPrice * i.quantity)}</strong></div>)}<div className="summary-line total"><span>Total</span><strong>{money(o.totalAmount)}</strong></div><p className="fineprint">{o.customerName} · {o.email}<br />{new Date(o.createdAt).toLocaleString()}</p></div>
      <aside className="summary"><h2>{finished ? "What’s next?" : "Complete your order"}</h2><ErrorMessage error={payment.error || start.error || simulate.error} />
        {!finished && payment.data && <>{payment.data.mode === "Demo" ? <><div className="demo-banner"><strong>Demo payment mode</strong><p>No card is charged. Choose a result to try the checkout flow.</p></div><button className="button full" disabled={simulate.isPending} onClick={() => simulate.mutate(true)}>Simulate successful payment</button><button className="secondary full" disabled={simulate.isPending} onClick={() => simulate.mutate(false)}>Simulate payment failure</button></> : <><p>Pay securely with Stripe Checkout in test mode.</p><button className="button full" disabled={start.isPending} onClick={() => start.mutate()}>{start.isPending ? "Opening checkout…" : "Continue to Stripe →"}</button><p className="fineprint">Returning from Stripe does not confirm payment. This page waits for the verified webhook.</p></>}</>}
        {!finished && !payment.data && <p>Preparing payment… This page updates automatically.</p>}
        {finished && <p>{o.status === "Confirmed" ? "Your bag has been updated. Explore the collection for your next upgrade." : "Reserved inventory will be released automatically. Your bag is ready to try again."}</p>}<Link href="/" className="continue">Back to collection →</Link></aside></div></>;
}
