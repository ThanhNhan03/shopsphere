"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useBasket, useSession } from "./store-provider";
import { api } from "@/lib/api";
import { Brand } from "./brand";
import { Icon } from "./icon";
export function Header() {
  const { data } = useBasket();
  const session = useSession();
  const client = useQueryClient();
  const router = useRouter();
  const path = usePathname();
  const logout = useMutation({
    mutationFn: () => api("/api/auth/logout", { method: "POST" }),
    onSuccess: async () => {
      await client.cancelQueries();
      client.setQueryData(["session"], {
        googleEnabled: session.data?.googleEnabled,
        user: null,
      });
      client.removeQueries({
        predicate: (query) =>
          !["session", "products", "product"].includes(
            String(query.queryKey[0]),
          ),
      });
      router.replace("/");
      router.refresh();
    },
  });
  const count = data?.items.reduce((sum, item) => sum + item.quantity, 0) || 0;
  return (
    <header className="header">
      <div className="header-inner">
        <Brand />
        <nav className="desktop-nav" aria-label="Main navigation">
          <Link href="/#collection" className={path === "/" ? "active" : ""}>
            The collection
          </Link>
          <Link href="/?category=Audio#collection">Audio</Link>
          <Link href="/?category=Accessories#collection">Desk essentials</Link>
        </nav>
        <div className="header-actions">
          {session.data?.user?.isAdmin && <Link href="/admin" className="sign-in-link">Admin</Link>}
          {session.data?.user ? (
            <div className="account-menu">
              <Link
                href="/login"
                className="account-name"
                title={session.data.user.email}
              >
                <span className="avatar">
                  {(session.data.user.name || session.data.user.email)
                    .charAt(0)
                    .toUpperCase()}
                </span>
                <span>
                  {session.data.user.name?.split(" ")[0] || "Account"}
                </span>
              </Link>
              <button
                className="icon-button logout-button"
                disabled={logout.isPending}
                onClick={() => logout.mutate()}
                aria-label="Sign out"
                title="Sign out"
              >
                <Icon name="logout" size={18} />
              </button>
            </div>
          ) : (
            <Link className="sign-in-link" href="/login">
              <Icon name="user" size={19} />
              <span>Sign in</span>
            </Link>
          )}
          <Link
            href="/cart"
            className="cart-link"
            aria-label={`Shopping bag, ${count} items`}
          >
            <Icon name="bag" size={20} />
            <span className="bag-label">Bag</span>
            <span className="bag-count">{count}</span>
          </Link>
        </div>
        <form className="header-search" action="/#collection" role="search">
          <Icon name="search" size={17} />
          <label className="sr-only" htmlFor="site-search">
            Search products
          </label>
          <input
            id="site-search"
            type="search"
            name="q"
            placeholder="Find your next essential"
          />
          <button type="submit" aria-label="Search products">
            <Icon name="arrow" size={16} />
          </button>
        </form>
        {logout.error && (
          <p role="alert" className="header-error">
            Could not sign out. Please try again.
          </p>
        )}
      </div>
    </header>
  );
}
