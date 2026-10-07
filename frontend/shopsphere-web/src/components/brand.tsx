import Link from "next/link";
export function Brand() {
  return (
    <Link href="/" className="logo" aria-label="ShopSphere home">
      <svg
        className="logo-mark"
        width="32"
        height="32"
        viewBox="0 0 32 32"
        fill="none"
        aria-hidden="true"
      >
        <circle cx="16" cy="16" r="13" stroke="currentColor" strokeWidth="2" />
        <ellipse
          cx="16"
          cy="16"
          rx="6"
          ry="13"
          stroke="currentColor"
          strokeWidth="2"
          transform="rotate(38 16 16)"
        />
        <path
          d="M3 16h26"
          stroke="currentColor"
          strokeWidth="2"
          transform="rotate(-24 16 16)"
        />
      </svg>
      shopsphere<span className="logo-dot">.</span>
    </Link>
  );
}
