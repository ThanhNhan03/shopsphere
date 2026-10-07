import Link from "next/link";
import { Icon } from "./icon";
export function CheckoutSteps({ active }: { active: number }) {
  return (
    <ol className="checkout-steps" aria-label="Checkout progress">
      {["Shopping bag", "Your details", "Payment"].map((label, i) => (
        <li
          key={label}
          className={i < active ? "done" : i === active ? "current" : ""}
          aria-current={i === active ? "step" : undefined}
        >
          <span>
            {i < active ? <Icon name="check" size={14} /> : `0${i + 1}`}
          </span>
          {i < active && i < 2 ? (
            <Link href={i === 0 ? "/cart" : "/checkout"}>{label}</Link>
          ) : (
            label
          )}
        </li>
      ))}
    </ol>
  );
}
