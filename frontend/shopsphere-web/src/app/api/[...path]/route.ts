import { NextRequest } from "next/server";
async function proxy(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;
  const url = new URL(`/api/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`, process.env.GATEWAY_URL || "http://localhost:8080");
  try {
    const response = await fetch(url, {
      method: request.method,
      headers: {
        "Content-Type": request.headers.get("content-type") || "application/json",
        Cookie: request.headers.get("cookie") || "",
        ...(request.headers.get("origin") ? { Origin: request.headers.get("origin")! } : {}),
      },
      redirect: "manual",
      body: ["GET", "HEAD"].includes(request.method) ? undefined : await request.arrayBuffer(),
      cache: "no-store",
      signal: AbortSignal.timeout(30000),
    });
    const headers = new Headers({ "Content-Type": response.headers.get("content-type") || "application/json", "Cache-Control": "no-store" });
    if (response.headers.has("location")) headers.set("Location", response.headers.get("location")!);
    if (response.headers.has("retry-after")) headers.set("Retry-After", response.headers.get("retry-after")!);
    for (const cookie of response.headers.getSetCookie()) headers.append("Set-Cookie", cookie);
    return new Response(response.body, { status: response.status, headers });
  } catch {
    return Response.json({ detail: "The store services are temporarily unavailable. Please try again." }, { status: 502 });
  }
}
export { proxy as GET, proxy as POST, proxy as PUT, proxy as DELETE };
