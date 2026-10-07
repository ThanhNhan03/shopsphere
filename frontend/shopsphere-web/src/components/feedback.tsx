export function ErrorMessage({ error }: { error: Error | null }) {
  return error ? <p role="alert" className="error">{error.message}</p> : null;
}
export function Loading() { return <p role="status" className="empty">Loading your store…</p>; }
