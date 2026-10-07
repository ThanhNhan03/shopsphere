import { Catalog } from "@/features/catalog/catalog";
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<{ q?: string; category?: string }>;
}) {
  const params = await searchParams;
  return (
    <Catalog
      key={`${params.q || ""}-${params.category || ""}`}
      initialQuery={params.q || ""}
      initialCategory={params.category || "All"}
    />
  );
}
