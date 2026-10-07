import { OrderDetail } from "@/features/orders/order-detail";
import Link from "next/link";
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ order_id?: string }>;
}) {
  const { order_id } = await searchParams;
  return order_id ? (
    <OrderDetail id={order_id} />
  ) : (
    <div className="empty">
      <p className="eyebrow">YOUR ORDER</p>
      <h1>Let’s find your order.</h1>
      <p>Open your order link to see the latest payment status.</p>
      <Link href="/" className="button">
        Back to the collection →
      </Link>
    </div>
  );
}
