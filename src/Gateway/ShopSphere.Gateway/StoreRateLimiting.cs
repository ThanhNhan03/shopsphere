using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace ShopSphere.Gateway;

public static class StoreRateLimiting
{
    public static void AddStoreRateLimiting(this WebApplicationBuilder builder)
    {
        var windowSeconds = Positive(builder.Configuration, "WindowSeconds", 60);
        var reads = Positive(builder.Configuration, "ReadPermitLimit", 120);
        var writes = Positive(builder.Configuration, "WritePermitLimit", 30);
        var login = Positive(builder.Configuration, "LoginPermitLimit", 10);
        var callbacks = Positive(builder.Configuration, "CallbackPermitLimit", 30);
        var webhook = Positive(builder.Configuration, "WebhookPermitLimit", 120);
        var ceiling = Positive(builder.Configuration, "ConnectionPermitLimit", 600);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(c => !c.Request.Path.StartsWithSegments("/api")
                    ? RateLimitPartition.GetNoLimiter("public")
                    : Window("connection:" + Source(c) + (IsWebhook(c) ? ":stripe" : ":store"), ceiling, windowSeconds)),
                PartitionedRateLimiter.Create<HttpContext, string>(c =>
                {
                    if (!c.Request.Path.StartsWithSegments("/api")) return RateLimitPartition.GetNoLimiter("public");
                    var source = Source(c);
                    if (IsWebhook(c)) return Window("webhook:" + source, webhook, windowSeconds);
                    if (Matches(c, "/api/auth/google") || Matches(c, "/api/auth/login") || Matches(c, "/api/auth/register")) return Window("login:" + source, login, windowSeconds);
                    if (Matches(c, "/api/auth/google/callback")) return Window("callback:" + source, callbacks, windowSeconds);
                    var identity = c.User.FindFirstValue("customer_id") is { } user ? "user:" + user : "connection:" + source;
                    var read = HttpMethods.IsGet(c.Request.Method) || HttpMethods.IsHead(c.Request.Method);
                    return Window((read ? "read:" : "write:") + identity, read ? reads : writes, windowSeconds);
                }));
            options.OnRejected = async (context, ct) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                    ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : windowSeconds;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    title = "Too many requests",
                    detail = $"Too many requests. Please try again in {seconds} seconds.",
                    status = StatusCodes.Status429TooManyRequests
                }, cancellationToken: ct);
            };
        });
    }

    // Never trust caller-supplied X-Forwarded-For. Local Next.js proxy traffic shares
    // its connection quota; authenticated account quotas remain independent.
    private static string Source(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    private static bool Matches(HttpContext c, string path) => string.Equals(c.Request.Path.Value?.TrimEnd('/'), path, StringComparison.OrdinalIgnoreCase);
    private static bool IsWebhook(HttpContext c) => Matches(c, "/api/payments/webhooks/stripe");
    private static RateLimitPartition<string> Window(string key, int permits, int seconds) =>
        RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permits, Window = TimeSpan.FromSeconds(seconds),
            SegmentsPerWindow = 6, QueueLimit = 0, AutoReplenishment = true
        });
    private static int Positive(IConfiguration config, string key, int fallback)
    {
        var value = config.GetValue<int?>("RateLimiting:" + key) ?? fallback;
        if (value < 1) throw new InvalidOperationException($"RateLimiting:{key} must be positive.");
        return value;
    }
}
