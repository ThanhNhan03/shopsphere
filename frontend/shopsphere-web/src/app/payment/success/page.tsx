import { OrderDetail } from "@/features/orders/order-detail";
export default async function Page({ searchParams }: { searchParams: Promise<{ order_id?: string }> }) { const { order_id } = await searchParams; return order_id ? <OrderDetail id={order_id} /> : <p>Open your order link to check payment status.</p>; }
