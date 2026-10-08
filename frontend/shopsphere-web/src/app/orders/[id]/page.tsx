import { OrderDetail } from "@/features/orders/order-detail";
export default async function Page({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ cancelled?: string }> }) {
  const { id } = await params;
  const { cancelled } = await searchParams;
  return <OrderDetail id={id} checkoutClosed={cancelled === "1"} />;
}
