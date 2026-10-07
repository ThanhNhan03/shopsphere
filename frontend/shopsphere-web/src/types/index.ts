export type Product = { id: string; name: string; description: string; price: number; imageUrl: string; brand: string; category: string };
export type BasketItem = { productId: string; name: string; unitPrice: number; quantity: number; imageUrl: string };
export type Basket = { customerId: string; items: BasketItem[]; total: number };
export type Order = { id: string; customerId: string; customerName: string; email: string; totalAmount: number; status: string; cancellationReason?: string; createdAt: string; items: BasketItem[] };
export type Payment = { orderId: string; status: string; amount: number; currency: string; checkoutUrl: string | null; mode: "Demo" | "Stripe" };
