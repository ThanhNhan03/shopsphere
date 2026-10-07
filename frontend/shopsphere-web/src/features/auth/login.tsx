"use client";
import Link from "next/link";
import { useSession } from "@/components/store-provider";
import { ErrorMessage, Loading } from "@/components/feedback";
import { Icon, GoogleMark } from "@/components/icon";
import { ProductImage } from "@/components/product-image";
import { deviceIcons } from "@/lib/device-icons";
export function Login({
  returnUrl,
  error,
}: {
  returnUrl: string;
  error?: string;
}) {
  const session = useSession();
  return (
    <div className="auth-layout">
      <section className="auth-story">
        <p className="eyebrow">YOUR EVERYDAY STARTS HERE</p>
        <h1>
          Good things.
          <br />
          Better <span className="slogan-accent">together.</span>
        </h1>
        <p>
          A place for your picks, your favorites,
          <br />
          and your next everyday upgrade.
        </p>
        <div className="auth-product">
          <div className="auth-orbit" />
          <ProductImage
            src={deviceIcons.headphones}
            name="Headphones"
            large
          />
        </div>
        <div className="auth-story-bottom">
          <Icon name="spark" size={24} />
          <span>
            THOUGHTFULLY CHOSEN.
            <br />
            SIMPLY YOURS.
          </span>
        </div>
      </section>
      <section className="auth-card">
        <Link href="/#collection" className="back">
          ← Back to the collection
        </Link>
        <div className="auth-heading-icon">
          <Icon name={session.data?.user ? "check" : "user"} size={26} />
        </div>
        <p className="eyebrow">
          {session.data?.user
            ? "RIGHT WHERE YOU BELONG"
            : "WELCOME TO SHOPSPHERE"}
        </p>
        <h2>
          {session.data?.user ? "You’re all set." : "Your next upgrade awaits."}
        </h2>
        <p>
          {session.data?.user
            ? "Your account is ready. Pick up where you left off."
            : "Sign in to save your bag and keep your essentials in one place."}
        </p>
        {session.isPending ? (
          <Loading />
        ) : session.data?.user ? (
          <>
            <div className="signed-in-account">
              <span className="avatar">
                {(session.data.user.name || session.data.user.email).charAt(0)}
              </span>
              <div>
                <strong>{session.data.user.name}</strong>
                <span>{session.data.user.email}</span>
              </div>
              <Icon name="check" size={18} />
            </div>
            <Link className="button full" href={returnUrl}>
              Continue shopping <Icon name="arrow" />
            </Link>
            <a
              className="account-switch"
              href={`/api/auth/google?returnUrl=${encodeURIComponent(returnUrl)}`}
            >
              Use a different Google account
            </a>
          </>
        ) : session.data?.googleEnabled ? (
          <>
            <a
              className="google-button"
              href={`/api/auth/google?returnUrl=${encodeURIComponent(returnUrl)}`}
            >
              <GoogleMark />
              Continue with Google
              <Icon name="arrow" size={18} />
            </a>
            <p className="auth-hint">
              Choose your account on the next screen.
              <br />
              Gmail and other Google accounts are welcome.
            </p>
          </>
        ) : (
          <p className="auth-notice">
            Google sign-in is not configured yet. Please contact the store
            administrator.
          </p>
        )}
        <ErrorMessage
          error={
            session.error ||
            (error === "google"
              ? new Error(
                  "Sign-in was cancelled or couldn’t be completed. Please try again.",
                )
              : null)
          }
        />
        <div className="auth-privacy">
          <Icon name="shield" size={19} />
          <p>
            Your password stays with Google.
            <br />
            <span>We only receive your basic profile and email.</span>
          </p>
        </div>
      </section>
    </div>
  );
}
