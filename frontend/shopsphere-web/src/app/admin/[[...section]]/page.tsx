import { notFound } from "next/navigation";
import { Admin } from "@/features/admin/admin";
export default async function AdminPage({ params }: { params: Promise<{ section?: string[] }> }) {
  const { section } = await params;
  const page = section?.[0] || "overview";
  if ((section?.length || 0) > 1 || !["overview", "products", "inventory", "orders", "payments"].includes(page)) notFound();
  return <Admin section={page} />;
}
