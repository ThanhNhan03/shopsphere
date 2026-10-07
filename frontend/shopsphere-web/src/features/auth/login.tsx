"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useSession, type AuthSession } from "@/components/store-provider";
import { ErrorMessage, Loading } from "@/components/feedback";
import { Icon, GoogleMark } from "@/components/icon";
import { ProductImage } from "@/components/product-image";
import { deviceIcons } from "@/lib/device-icons";
import { api, json } from "@/lib/api";
export function Login({ returnUrl, error, register = false }: { returnUrl: string; error?: string; register?: boolean }) {
  const session = useSession();
  const client = useQueryClient();
  const router = useRouter();
  const submit = useMutation({
    mutationFn: async (form: FormData) => {
      await api(`/api/auth/${register ? "register" : "login"}`, json("POST", Object.fromEntries(form)));
      return api<AuthSession>("/api/auth/session");
    },
    onSuccess: async (result) => {
      await client.cancelQueries();
      client.removeQueries({ predicate: q => !["session", "products", "product"].includes(String(q.queryKey[0])) });
      client.setQueryData(["session"], result);
      router.replace(returnUrl === "/" && result.user?.isAdmin ? "/admin" : returnUrl);
      router.refresh();
    },
  });
  const destination = returnUrl === "/" && session.data?.user?.isAdmin ? "/admin" : returnUrl;
  return <div className="auth-layout">
    <section className="auth-story">
      <p className="eyebrow">YOUR EVERYDAY STARTS HERE</p>
      <h1>Good things.<br />Better <span className="slogan-accent">together.</span></h1>
      <p>A place for your picks, your favorites,<br />and your next everyday upgrade.</p>
      <div className="auth-product"><div className="auth-orbit" /><ProductImage src={deviceIcons.headphones} name="Headphones" large /></div>
      <div className="auth-story-bottom"><Icon name="spark" size={24} /><span>THOUGHTFULLY CHOSEN.<br />SIMPLY YOURS.</span></div>
    </section>
    <section className="auth-card">
      <Link href="/#collection" className="back">← Back to the collection</Link>
      <div className="auth-heading-icon"><Icon name="user" size={26} /></div>
      <p className="eyebrow">WELCOME TO SHOPSPHERE</p>
      <h2>{session.data?.user ? "You’re all set." : register ? "Make yourself at home." : "Welcome back."}</h2>
      <p>{register ? "Create an account to save your bag and place orders." : "Sign in to pick up where you left off."}</p>
      {session.isPending ? <Loading /> : session.data?.user ? <>
        <div className="signed-in-account"><span className="avatar">{session.data.user.name.charAt(0)}</span><div><strong>{session.data.user.name}</strong><span>{session.data.user.email}</span></div><Icon name="check" size={18} /></div>
        <Link className="button full" href={destination}>{destination.startsWith("/admin") ? "Open administration" : "Continue shopping"}<Icon name="arrow" /></Link>
      </> : <>
        <form className="account-form" onSubmit={e => { e.preventDefault(); submit.mutate(new FormData(e.currentTarget)); }}>
          {register && <label>Full name<input name="name" autoComplete="name" required maxLength={100} /></label>}
          <label>Email address<input name="email" type="email" autoComplete="email" required maxLength={254} /></label>
          <label>Password<input name="password" type="password" autoComplete={register ? "new-password" : "current-password"} required minLength={register ? 10 : 1} maxLength={128} /></label>
          {register && <p className="input-hint">Use 10–128 characters. Your account is created as a customer.</p>}
          <ErrorMessage error={submit.error} />
          <button className="button full" disabled={submit.isPending}>{submit.isPending ? "Please wait…" : register ? "Create account" : "Sign in"}<Icon name="arrow" /></button>
        </form>
        <p className="account-switch">{register ? "Already have an account? " : "New here? "}<Link href={`${register ? "/login" : "/register"}?returnUrl=${encodeURIComponent(returnUrl)}`}>{register ? "Sign in" : "Create an account"}</Link></p>
        {session.data?.googleEnabled && <><div className="auth-divider">or</div><a className="google-button" href={`/api/auth/google?returnUrl=${encodeURIComponent(returnUrl)}`}><GoogleMark />Continue with Google<Icon name="arrow" size={18} /></a></>}
      </>}
      <ErrorMessage error={session.error || (error === "google" ? new Error("Google sign-in could not be completed. Please try again.") : null)} />
      <div className="auth-privacy"><Icon name="shield" size={19} /><p>Your account keeps your bag and orders together.</p></div>
    </section>
  </div>;
}
