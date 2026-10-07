"use client";
import Link from "next/link";
import { useSession } from "./store-provider";
import { ErrorMessage, Loading } from "./feedback";
import { Icon } from "./icon";
export function SignInPrompt({ returnUrl }: { returnUrl: string }) {
  const session = useSession();
  if (session.isPending) return <Loading />;
  if (session.error) return <ErrorMessage error={session.error} />;
  return (
    <div className="empty sign-in-empty">
      <span className="empty-icon">
        <Icon name="bag" size={32} />
      </span>
      <p className="eyebrow">A SPACE FOR YOUR ESSENTIALS</p>
      <h1>
        Your account.
        <br />
        Your shopping bag.
      </h1>
      <p>
        Sign in with Google to save your picks
        <br />
        and keep your orders in one place.
      </p>
      <Link
        className="button"
        href={`/login?returnUrl=${encodeURIComponent(returnUrl)}`}
      >
        Sign in to continue <Icon name="arrow" />
      </Link>
      <Link className="continue" href="/#collection">
        Explore the collection first
      </Link>
    </div>
  );
}
