"use client";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSession } from "@/components/store-provider";
import { Brand } from "@/components/brand";
import { Icon, type IconName } from "@/components/icon";
import { ErrorMessage, Loading } from "@/components/feedback";
import { ProductImage } from "@/components/product-image";
import { api, json, money } from "@/lib/api";
import type { Product, Order } from "@/types";
import "./admin.css";

type Stock = { productId: string; availableQuantity: number; reservedQuantity: number };
type Adjustment = { id: string; delta: number; availableAfter: number; reason: string; performedBy: string; createdAt: string };
type Summary = { totalOrders: number; pendingOrders: number; confirmedOrders: number; cancelledOrders: number; revenue: number };
type OrderPage = { items: Order[]; total: number; page: number; pageSize: number };
const navigation: { key: string; label: string; icon: IconName }[] = [
  { key: "overview", label: "Overview", icon: "spark" }, { key: "products", label: "Products", icon: "box" },
  { key: "inventory", label: "Inventory", icon: "bag" }, { key: "orders", label: "Orders", icon: "check" },
];
const date = (value: string) => new Date(value).toLocaleString("en-US", { dateStyle: "medium", timeStyle: "short" });
function Badge({ value }: { value: string }) { return <span className={`admin-badge ${value.toLowerCase()}`}>{value.replace(/([a-z])([A-Z])/g, "$1 $2")}</span>; }
function Dialog({ title, close, busy = false, children }: { title: string; close: () => void; busy?: boolean; children: React.ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => { const dialog = ref.current!; dialog.showModal(); return () => dialog.close(); }, []);
  return <dialog ref={ref} className="admin-dialog" aria-labelledby="dialog-title" onCancel={e => { e.preventDefault(); if (!busy) close(); }}>
    <div className="admin-dialog-heading"><h2 id="dialog-title">{title}</h2><button type="button" className="icon-button" onClick={close} disabled={busy} aria-label="Close dialog"><Icon name="close" /></button></div>{children}
  </dialog>;
}
export function Admin({ section }: { section: string }) {
  const session = useSession();
  if (session.isPending) return <div className="admin-root admin-gate"><Loading /></div>;
  if (session.error) return <div className="admin-root admin-gate"><ErrorMessage error={session.error} /></div>;
  if (!session.data?.user) return <div className="admin-root admin-gate"><Brand /><h1>Store administration</h1><p>Sign in with your administrator account to continue.</p><Link className="button" href={`/login?returnUrl=${encodeURIComponent(`/admin/${section === "overview" ? "" : section}`)}`}>Sign in</Link></div>;
  if (!session.data.user.isAdmin) return <div className="admin-root admin-gate"><h1>Administrator access required</h1><p>Your account has customer access.</p><Link className="button" href="/">Back to the store</Link></div>;
  return <AdminWorkspace section={section} email={session.data.user.email} />;
}
function AdminWorkspace({ section, email }: { section: string; email: string }) {
  const client = useQueryClient(); const router = useRouter();
  const products = useQuery({ queryKey: ["admin", "products"], queryFn: () => api<Product[]>("/api/admin/products") });
  const stocks = useQuery({ queryKey: ["admin", "inventory"], queryFn: () => api<Stock[]>("/api/admin/inventory"), refetchInterval: 15000 });
  const summary = useQuery({ queryKey: ["admin", "summary"], queryFn: () => api<Summary>("/api/admin/orders/summary"), refetchInterval: 15000 });
  const logout = useMutation({ mutationFn: () => api("/api/auth/logout", { method: "POST" }), onSuccess: async () => { await client.cancelQueries(); client.clear(); router.replace("/login"); router.refresh(); } });
  const [editProduct, setEditProduct] = useState<Product | "new" | null>(null);
  const [editStock, setEditStock] = useState<Product | null>(null);
  const [query, setQuery] = useState(""); const [active, setActive] = useState("all");
  const list = (products.data || []).filter(p => `${p.name} ${p.brand} ${p.category}`.toLowerCase().includes(query.toLowerCase()) && (active === "all" || p.isActive === (active === "active")));
  const stockFor = (id: string): Stock => stocks.data?.find(s => s.productId === id) || { productId: id, availableQuantity: 0, reservedQuantity: 0 };
  const low = (products.data || []).filter(p => p.isActive && stockFor(p.id).availableQuantity <= 5);
  const heading = navigation.find(n => n.key === section)!.label;
  return <div className="admin-root">
    <aside className="admin-sidebar"><Brand /><span className="admin-store-label">STORE MANAGEMENT</span><nav aria-label="Administration">{navigation.map(n => <Link key={n.key} href={n.key === "overview" ? "/admin" : `/admin/${n.key}`} className={section === n.key ? "selected" : ""} aria-current={section === n.key ? "page" : undefined}><Icon name={n.icon} />{n.label}</Link>)}</nav><div className="admin-sidebar-bottom"><Link href="/">Visit storefront <Icon name="arrow-up" size={16} /></Link><span>{email}</span><button onClick={() => logout.mutate()} disabled={logout.isPending}><Icon name="logout" size={17} />Sign out</button><ErrorMessage error={logout.error} /></div></aside>
    <div className="admin-content"><header className="admin-top"><div><p className="eyebrow">SHOPSPHERE / ADMINISTRATION</p><h1>{heading}</h1></div><span className="admin-role"><Icon name="shield" size={17} /> Administrator</span></header>
      <ErrorMessage error={products.error || stocks.error || summary.error} />
      {section === "overview" && <>
        <p className="admin-intro">A clear view of your catalog, stock, and orders.</p>
        <div className="admin-stats">{[
          ["Confirmed revenue", summary.data ? money(summary.data.revenue) : "—", "Confirmed orders only"], ["Orders", summary.data?.totalOrders ?? "—", `${summary.data?.pendingOrders ?? "—"} in progress`],
          ["Active products", products.data?.filter(p => p.isActive).length ?? "—", `${products.data?.length ?? "—"} in the catalog`], ["Low stock", products.data && stocks.data ? low.length : "—", "5 available units or fewer"],
        ].map(([label, value, hint]) => <article key={label} className="admin-stat"><span>{label}</span><strong>{value}</strong><small>{hint}</small></article>)}</div>
        <div className="admin-overview-grid"><section className="admin-panel"><div className="admin-panel-title"><h2>Stock needs attention</h2><Link href="/admin/inventory">Manage stock →</Link></div>{products.isPending || stocks.isPending ? <Loading /> : !low.length ? <p className="admin-empty">All active products have more than 5 available units.</p> : low.map(p => <div className="admin-attention" key={p.id}><ProductImage src={p.imageUrl} name={p.name} category={p.category} /><div><strong>{p.name}</strong><span>{p.category}</span></div><Badge value={stockFor(p.id).availableQuantity === 0 ? "Empty" : "Low"} /><b>{stockFor(p.id).availableQuantity}</b></div>)}</section>
        <section className="admin-panel"><h2>Order activity</h2>{summary.isPending ? <Loading /> : <><div className="admin-summary-row"><span>Confirmed</span><b>{summary.data?.confirmedOrders ?? "—"}</b></div><div className="admin-summary-row"><span>In progress</span><b>{summary.data?.pendingOrders ?? "—"}</b></div><div className="admin-summary-row"><span>Cancelled</span><b>{summary.data?.cancelledOrders ?? "—"}</b></div><Link className="button full" href="/admin/orders">Review orders <Icon name="arrow" /></Link></>}<p className="admin-note">Order states follow inventory and payment events.</p></section></div>
      </>}
      {(section === "products" || section === "inventory") && <>
        <p className="admin-intro">{section === "products" ? "Edit your collection, upload photos, and control product visibility." : "Adjust available stock while keeping reserved units protected."}</p>
        <div className="admin-toolbar"><label className="admin-search"><Icon name="search" size={18} /><input aria-label="Search products" placeholder="Search name, brand, or category" value={query} onChange={e => setQuery(e.target.value)} /></label><select aria-label="Product visibility" value={active} onChange={e => setActive(e.target.value)}><option value="all">All products</option><option value="active">Active products</option><option value="inactive">Hidden products</option></select>{section === "products" && <button className="button" onClick={() => setEditProduct("new")}><Icon name="plus" size={17} />Add product</button>}</div>
        {products.isPending || (section === "inventory" && stocks.isPending) ? <Loading /> : <div className="admin-panel admin-table-wrap"><table><thead><tr><th>Product</th><th>{section === "products" ? "Price" : "Available"}</th><th>{section === "products" ? "Visibility" : "Reserved"}</th><th>{section === "products" ? "Category" : "Stock level"}</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{list.map(p => { const stock = stockFor(p.id); return <tr key={p.id}><td><div className="admin-product-cell"><ProductImage src={p.imageUrl} name={p.name} category={p.category} /><div><strong>{p.name}</strong><small>{p.brand}</small></div></div></td><td>{section === "products" ? money(p.price) : stock.availableQuantity}</td><td>{section === "products" ? <Badge value={p.isActive ? "Active" : "Hidden"} /> : stock.reservedQuantity}</td><td>{section === "products" ? p.category : <Badge value={stock.availableQuantity === 0 ? "Empty" : stock.availableQuantity <= 5 ? "Low" : "Healthy"} />}</td><td><button className="admin-secondary" onClick={() => section === "products" ? setEditProduct(p) : setEditStock(p)}>{section === "products" ? "Edit" : "Adjust stock"}</button></td></tr>; })}</tbody></table>{!list.length && <p className="admin-empty">No products match this view.</p>}<div className="admin-table-footer">{list.length} products</div></div>}
      </>}
      {section === "orders" && <Orders />}
    </div>
    {editProduct && <ProductEditor product={editProduct === "new" ? null : editProduct} close={() => setEditProduct(null)} />}
    {editStock && <StockEditor product={editStock} stock={stockFor(editStock.id)} close={() => setEditStock(null)} />}
  </div>;
}
function ProductEditor({ product, close }: { product: Product | null; close: () => void }) {
  const client = useQueryClient(); const [saved, setSaved] = useState(product);
  const save = useMutation({ mutationFn: async (form: FormData) => {
    const input = { name: form.get("name"), description: form.get("description"), brand: form.get("brand"), category: form.get("category"), price: Number(form.get("price")), isActive: form.get("active") === "on", version: saved?.version || 0 };
    const file = form.get("image") as File;
    if (file.size && (file.size > 5 * 1024 * 1024 || !["image/jpeg", "image/png", "image/webp"].includes(file.type))) throw new Error("Choose a PNG, JPEG, or WebP image up to 5 MB.");
    const result = await api<Product>(saved ? `/api/admin/products/${saved.id}` : "/api/admin/products", json(saved ? "PUT" : "POST", input));
    setSaved(result);
    if (file.size) {
      const upload = new FormData(); upload.set("image", file); upload.set("version", String(result.version));
      try { setSaved(await api<Product>(`/api/admin/products/${result.id}/image`, { method: "POST", body: upload })); }
      catch (error) { throw new Error(`Product details saved. Image upload failed: ${(error as Error).message} You can retry here.`); }
    }
  }, onSuccess: close, onSettled: () => { client.invalidateQueries({ queryKey: ["admin"] }); client.invalidateQueries({ queryKey: ["products"] }); client.invalidateQueries({ queryKey: ["product"] }); } });
  return <Dialog title={product ? "Edit product" : "Add product"} close={close} busy={save.isPending}><form className="admin-form" onSubmit={e => { e.preventDefault(); save.mutate(new FormData(e.currentTarget)); }}>
    <label>Product name<input name="name" required maxLength={150} defaultValue={product?.name} /></label>
    <div className="admin-form-grid"><label>Brand<input name="brand" required maxLength={100} defaultValue={product?.brand} /></label><label>Category<input name="category" required maxLength={100} defaultValue={product?.category} /></label></div>
    <label>Price (USD)<input name="price" type="number" min="0.01" max="1000000" step="0.01" required defaultValue={product?.price} /></label>
    <label>Description<textarea name="description" required maxLength={5000} rows={4} defaultValue={product?.description} /></label>
    <label>Product photo<input name="image" type="file" accept="image/png,image/jpeg,image/webp" /></label><p className="admin-note">PNG, JPEG, or WebP · Up to 5 MB. Uploading replaces the existing photo.</p>
    {saved?.imageUrl && <div className="admin-image-preview"><ProductImage src={saved.imageUrl} name={saved.name} category={saved.category} /></div>}
    <label className="admin-checkbox"><input name="active" type="checkbox" defaultChecked={product?.isActive ?? false} />Visible in the storefront</label><p className="admin-note">New products start hidden. Add stock and a photo before making them visible.</p>
    <ErrorMessage error={save.error} /><div className="admin-form-actions"><button type="button" className="admin-secondary" onClick={close} disabled={save.isPending}>Cancel</button><button className="button" disabled={save.isPending}>{save.isPending ? "Saving…" : "Save product"}</button></div>
  </form></Dialog>;
}
function StockEditor({ product, stock, close }: { product: Product; stock: Stock; close: () => void }) {
  const client = useQueryClient();
  const history = useQuery({ queryKey: ["admin", "history", product.id], queryFn: () => api<Adjustment[]>(`/api/admin/inventory/${product.id}/adjustments`) });
  const save = useMutation({ mutationFn: (form: FormData) => api(`/api/admin/inventory/${product.id}/adjustments`, json("POST", { delta: Number(form.get("delta")), expectedAvailable: stock.availableQuantity, reason: form.get("reason") })), onSuccess: close, onSettled: () => client.invalidateQueries({ queryKey: ["admin"] }) });
  return <Dialog title={`Adjust stock · ${product.name}`} close={close} busy={save.isPending}><form className="admin-form" onSubmit={e => { e.preventDefault(); save.mutate(new FormData(e.currentTarget)); }}>
    <div className="admin-stock-current"><span>Available <b>{stock.availableQuantity}</b></span><span>Reserved <b>{stock.reservedQuantity}</b></span></div>
    <label>Quantity change<input name="delta" type="number" step="1" min={-stock.availableQuantity} max={1000000 - stock.availableQuantity} placeholder="e.g. 10 or -2" required /></label><p className="admin-note">Positive adds stock; negative removes available units. Reserved units stay protected.</p>
    <label>Reason<textarea name="reason" maxLength={250} rows={2} required placeholder="Restock, damage, or stock correction" /></label><ErrorMessage error={save.error} />
    <div className="admin-form-actions"><button type="button" className="admin-secondary" onClick={close} disabled={save.isPending}>Cancel</button><button className="button" disabled={save.isPending}>{save.isPending ? "Saving…" : "Save adjustment"}</button></div>
  </form><div className="admin-history"><h3>Recent adjustments</h3>{history.isPending ? <Loading /> : <><ErrorMessage error={history.error} />{!history.data?.length && <p className="admin-note">No adjustments recorded yet.</p>}{history.data?.map(h => <article key={h.id}><b>{h.delta > 0 ? "+" : ""}{h.delta}</b><div><strong>{h.reason}</strong><small>{h.performedBy} · {date(h.createdAt)}</small></div><span>{h.availableAfter} after</span></article>)}</>}</div></Dialog>;
}
function Orders() {
  const [status, setStatus] = useState(""); const [query, setQuery] = useState(""); const [page, setPage] = useState(1); const [selected, setSelected] = useState<string | null>(null);
  const orders = useQuery({ queryKey: ["admin", "orders", status, query, page], queryFn: () => api<OrderPage>(`/api/admin/orders?${new URLSearchParams({ status, q: query, page: String(page) })}`), refetchInterval: 10000 });
  return <><p className="admin-intro">Follow every order from reservation to payment. States update automatically.</p><div className="admin-toolbar"><form className="admin-search" onSubmit={e => { e.preventDefault(); setQuery(String(new FormData(e.currentTarget).get("q") || "")); setPage(1); }}><Icon name="search" size={18} /><input name="q" aria-label="Search orders" placeholder="Order ID, customer, or email" maxLength={100} /><button className="admin-secondary">Search</button></form><select aria-label="Order status" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">All statuses</option>{["Pending", "AwaitingPayment", "Confirmed", "Cancelled"].map(s => <option key={s} value={s}>{s.replace(/([a-z])([A-Z])/g, "$1 $2")}</option>)}</select></div><ErrorMessage error={orders.error} />
  {orders.isPending ? <Loading /> : <div className="admin-panel admin-table-wrap"><table><thead><tr><th>Order / placed</th><th>Customer</th><th>Status</th><th>Total</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{orders.data?.items.map(o => <tr key={o.id}><td><strong title={o.id}>#{o.id.slice(0, 8)}</strong><small>{date(o.createdAt)}</small></td><td><strong>{o.customerName}</strong><small>{o.email}</small></td><td><Badge value={o.status} /></td><td>{money(o.totalAmount)}</td><td><button className="admin-secondary" onClick={() => setSelected(o.id)}>View details</button></td></tr>)}</tbody></table>{!orders.data?.items.length && <p className="admin-empty">No orders match this view.</p>}<div className="admin-table-footer"><span>{orders.data?.total || 0} orders · Page {page}</span><div><button className="admin-secondary" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</button><button className="admin-secondary" disabled={!orders.data || page * orders.data.pageSize >= orders.data.total} onClick={() => setPage(p => p + 1)}>Next</button></div></div></div>}
  {selected && <OrderDetail id={selected} close={() => setSelected(null)} />}</>;
}
function OrderDetail({ id, close }: { id: string; close: () => void }) {
  const order = useQuery({ queryKey: ["admin", "order", id], queryFn: () => api<Order>(`/api/admin/orders/${id}`), refetchInterval: 5000 }); const o = order.data;
  return <Dialog title="Order details" close={close}>{order.isPending ? <Loading /> : <><ErrorMessage error={order.error} />{o && <div className="admin-order-detail"><div className="admin-order-id">#{o.id}<Badge value={o.status} /></div><dl><dt>Customer</dt><dd>{o.customerName}</dd><dt>Email</dt><dd>{o.email}</dd><dt>Placed</dt><dd>{date(o.createdAt)}</dd></dl>{o.cancellationReason && <p role="status" className="admin-note">Cancellation: {o.cancellationReason}</p>}<table><thead><tr><th>Product</th><th>Qty</th><th>Total</th></tr></thead><tbody>{o.items.map(i => <tr key={i.productId}><td>{i.name}<small>{money(i.unitPrice)} each</small></td><td>{i.quantity}</td><td>{money(i.unitPrice * i.quantity)}</td></tr>)}</tbody></table><div className="admin-summary-row"><strong>Order total</strong><b>{money(o.totalAmount)}</b></div><p className="admin-note">Inventory and payment events determine this status. Confirmed revenue does not include cancelled or unpaid orders.</p></div>}</>}</Dialog>;
}
