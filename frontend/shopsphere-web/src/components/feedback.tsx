import { Icon } from "./icon";
export function ErrorMessage({ error }: { error: Error | null }) {
  return error ? (
    <div role="alert" className="error">
      <Icon name="close" size={18} />
      <span>{error.message}</span>
    </div>
  ) : null;
}
export function Loading() {
  return (
    <div role="status" className="loading-state">
      <span className="loading-orbit" />
      <p>Getting everything ready…</p>
    </div>
  );
}
