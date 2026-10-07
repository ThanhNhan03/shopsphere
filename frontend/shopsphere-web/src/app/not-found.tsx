import Link from "next/link";
export default function NotFound() {
  return (
    <div className="empty">
      <p className="eyebrow">A LITTLE OFF TRACK / 404</p>
      <h1>This page isn’t in our collection.</h1>
      <p>Let’s get you back to something good.</p>
      <Link href="/#collection" className="button">
        Explore the collection →
      </Link>
    </div>
  );
}
