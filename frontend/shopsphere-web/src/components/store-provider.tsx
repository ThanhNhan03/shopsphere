"use client";
import { createContext, useContext, useState } from "react";
import { QueryClient, QueryClientProvider, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, json } from "@/lib/api";
import type { Basket } from "@/types";

const Customer = createContext<string | null>(null);
export function StoreProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(() => new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 10000 } } }));
  return <QueryClientProvider client={client}><CustomerProvider>{children}</CustomerProvider></QueryClientProvider>;
}
function CustomerProvider({ children }: { children: React.ReactNode }) {
  const customer = useQuery({ queryKey: ["customer"], staleTime: Infinity, queryFn: () => {
    let id = localStorage.getItem("shopsphere-customer");
    if (!id) { id = crypto.randomUUID(); localStorage.setItem("shopsphere-customer", id); }
    return id;
  } });
  return <Customer.Provider value={customer.data || null}>{children}</Customer.Provider>;
}
export const useCustomer = () => useContext(Customer);
export function useBasket() {
  const customer = useCustomer();
  return useQuery({ queryKey: ["basket", customer], queryFn: () => api<Basket>(`/api/basket/${customer}`), enabled: !!customer, refetchInterval: 5000 });
}
export function useBasketChange() {
  const customer = useCustomer();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, quantity, method }: { productId: string; quantity?: number; method: "POST" | "PUT" | "DELETE" }) =>
      api(`/api/basket/${customer}/items${method === "POST" ? "" : `/${productId}`}`, method === "DELETE" ? { method } : json(method, { productId, quantity })),
    onSuccess: () => client.invalidateQueries({ queryKey: ["basket", customer] }),
  });
}
