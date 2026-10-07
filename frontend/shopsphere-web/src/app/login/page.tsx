import { Login } from "@/features/auth/login";
export default async function LoginPage({ searchParams }: { searchParams: Promise<{ returnUrl?: string; error?: string }> }) {
  const params = await searchParams;
  const destination = params.returnUrl;
  const returnUrl = destination?.startsWith("/") && !destination.startsWith("//") && !destination.includes("\\") ? destination : "/";
  return <Login returnUrl={returnUrl} error={params.error} />;
}
