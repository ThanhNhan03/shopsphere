import { Login } from "@/features/auth/login";
export default async function RegisterPage({ searchParams }: { searchParams: Promise<{ returnUrl?: string }> }) {
  const { returnUrl: destination } = await searchParams;
  const returnUrl = destination?.startsWith("/") && !destination.startsWith("//") && !destination.includes("\\") ? destination : "/";
  return <Login returnUrl={returnUrl} register />;
}
