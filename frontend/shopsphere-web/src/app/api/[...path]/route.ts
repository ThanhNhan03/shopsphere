import { NextRequest } from "next/server";
async function proxy(request: NextRequest, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;
  const url = new URL(`/api/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`, process.env.GATEWAY_URL || "http://localhost:8080");
  try {
    const limit = 5 * 1024 * 1024 + 65536;
    if (Number(request.headers.get("content-length")) > limit) return Response.json({ detail: "The upload is too large (max 5 MB)." }, { status: 413 });
    let body: Uint8Array | undefined;
    if (!["GET", "HEAD"].includes(request.method) && request.body) {
      const reader = request.body.getReader();
      const chunks: Uint8Array[] = []; let size = 0;
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        size += value.byteLength;
        if (size > limit) { await reader.cancel(); return Response.json({ detail: "The upload is too large (max 5 MB)." }, { status: 413 }); }
        chunks.push(value);
      }
      body = new Uint8Array(size); let offset = 0;
      for (const chunk of chunks) { body.set(chunk, offset); offset += chunk.length; }
    }
    const response = await fetch(url, {
      method: request.method,
      headers: {
        "Content-Type": request.headers.get("content-type") || "application/json",
        Cookie: request.headers.get("cookie") || "",
        ...(request.headers.get("origin") ? { Origin: request.headers.get("origin")! } : {}),
      },
      redirect: "manual",
      body: body as BodyInit | undefined,
      cache: "no-store",
      signal: AbortSignal.timeout(30000),
    });
    const headers = new Headers({ "Content-Type": response.headers.get("content-type") || "application/json", "Cache-Control": "no-store" });
    if (path[0] === "media" && response.ok) {
      headers.set("Cache-Control", response.headers.get("cache-control") || "public, max-age=3600");
      headers.set("X-Content-Type-Options", "nosniff");
    }
    if (response.headers.has("location")) headers.set("Location", response.headers.get("location")!);
    if (response.headers.has("retry-after")) headers.set("Retry-After", response.headers.get("retry-after")!);
    for (const cookie of response.headers.getSetCookie()) headers.append("Set-Cookie", cookie);
    return new Response(response.body, { status: response.status, headers });
  } catch {
    return Response.json({ detail: "The store services are temporarily unavailable. Please try again." }, { status: 502 });
  }
}
export { proxy as GET, proxy as POST, proxy as PUT, proxy as DELETE };
