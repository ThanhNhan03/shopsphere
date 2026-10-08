"use client";
import { createContext, useContext, useState } from "react";
import { QueryClient, QueryClientProvider, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, json } from "@/lib/api";
import type { Basket } from "@/types";
import { useRouter } from "next/navigation";

const Customer = createContext<string | null>(null);
export type AuthSession = { googleEnabled: boolean; user: { customerId: string; name: string; email: string; isAdmin: boolean; provider: string } | null };
export function useSession() {
  return useQuery({ queryKey: ["session"], queryFn: () => api<AuthSession>("/api/auth/session"), staleTime: 30000, retry: 1 });
}
export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(() => new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 10000 } } }));
  return <QueryClientProvider client={client}><CustomerProvider>{children}</CustomerProvider></QueryClientProvider>;
}
function CustomerProvider({ children }: { children: React.ReactNode }) {
  const session = useSession();
  return <Customer.Provider value={session.data?.user?.customerId || null}>{children}</Customer.Provider>;
}
export const useCustomer = () => useContext(Customer);
export function useBasket() {
  const customer = useCustomer();
  return useQuery({ queryKey: ["basket", customer], queryFn: ({ signal }) => api<Basket>(`/api/basket/${customer}`, { signal }), enabled: !!customer, refetchInterval: 5000 });
}
export function useBasketChange() {
  const router = useRouter();
  const customer = useCustomer();
  const client = useQueryClient();
  return useMutation({
    mutationKey: ["basket-change", customer],
    scope: { id: `basket-${customer}` },
    onMutate: () => client.cancelQueries({ queryKey: ["basket", customer] }),
    mutationFn: ({ productId, quantity, method }: { productId: string; quantity?: number; method: "POST" | "PUT" | "DELETE" }) => {
      if (!customer) { router.push(`/login?returnUrl=${encodeURIComponent(window.location.pathname)}`); throw new Error("Sign in to add items to your bag."); }
      return api<Basket | undefined>(`/api/basket/${customer}/items${method === "POST" ? "" : `/${productId}`}`, method === "DELETE" ? { method } : json(method, { productId, quantity }));
    },
    onSuccess: (result, variables) => {
      const key = ["basket", customer];
      if (result) { client.setQueryData(key, result); return; }
      if (variables.method === "DELETE") {
        client.setQueryData<Basket>(key, (previous) => {
          if (!previous) return previous;
          const items = previous.items.filter((item) => item.productId !== variables.productId);
          return { ...previous, items, total: items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0),
            canCheckout: items.length > 0 && items.every((item) => item.isAvailable && item.quantity <= item.availableQuantity) };
        });
      }
    },
    onError: () => client.invalidateQueries({ queryKey: ["basket", customer] }),
  });
}
