"use client";
import { usePathname } from "next/navigation";
export function StoreChrome({ children }: { children: React.ReactNode }) {
  return usePathname().startsWith("/admin") ? null : children;
}
