using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace ShopSphere.Gateway;
public static class StoreAuthentication
{
    internal const string Scheme = "Store";
    private static bool Configured(IConfiguration c) => !string.IsNullOrWhiteSpace(c["Google:ClientId"]) && !string.IsNullOrWhiteSpace(c["Google:ClientSecret"]);
    public static bool IsAdministrator(ClaimsPrincipal user, IConfiguration configuration) =>
        user.Identity?.IsAuthenticated == true && (user.HasClaim("local_admin", "true") ||
        (user.FindFirstValue("email_verified") == "true" &&
        (configuration["Admin:Emails"] ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(user.FindFirstValue(ClaimTypes.Email), StringComparer.OrdinalIgnoreCase)));
    public static void AddStoreAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddDataProtection().SetApplicationName("ShopSphere").PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Auth:KeyPath"] ?? ".auth-keys"));
        builder.Services.AddHttpClient("ownership");
        var auth = builder.Services.AddAuthentication(Scheme).AddCookie(Scheme, o =>
        {
            o.Cookie.Name = "shopsphere-session"; o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false;
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        if (Configured(builder.Configuration)) auth.AddGoogle(o =>
        {
            o.SignInScheme = Scheme; o.ClientId = builder.Configuration["Google:ClientId"]!; o.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
            o.CallbackPath = "/api/auth/google/callback"; o.UsePkce = true; o.SaveTokens = false;
            o.CorrelationCookie.SameSite = SameSiteMode.Lax; o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.Events.OnRedirectToAuthorizationEndpoint = c =>
            {
                c.Response.Redirect(QueryHelpers.AddQueryString(c.RedirectUri, "prompt", "select_account"));
                return Task.CompletedTask;
            };
            o.Events.OnCreatingTicket = async c =>
            {
                var subject = c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing Google account ID.");
                var email = c.User.TryGetProperty("email", out var emailValue) ? emailValue.GetString() : c.Principal?.FindFirstValue(ClaimTypes.Email);
                var name = c.User.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : c.Principal?.FindFirstValue(ClaimTypes.Name);
                var picture = c.User.TryGetProperty("picture", out var pictureValue) ? pictureValue.GetString() : null;
                var verified = (c.User.TryGetProperty("verified_email", out var verifiedValue) || c.User.TryGetProperty("email_verified", out verifiedValue)) && verifiedValue.ValueKind == JsonValueKind.True;
                var account = await c.HttpContext.RequestServices.GetRequiredService<Accounts>()
                    .SignInWithGoogle(subject, email ?? "", name ?? "", picture, verified, c.HttpContext.RequestAborted);
                var identity = (ClaimsIdentity)c.Principal!.Identity!;
                foreach (var claim in identity.FindAll(ClaimTypes.NameIdentifier).Concat(identity.FindAll(ClaimTypes.Name)).Concat(identity.FindAll(ClaimTypes.Email))
                             .Concat(identity.FindAll("customer_id")).Concat(identity.FindAll("provider")).Concat(identity.FindAll("local_admin")).Concat(identity.FindAll("email_verified")).ToArray())
                    identity.RemoveClaim(claim);
                identity.AddClaims([
                    new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                    new Claim(ClaimTypes.Name, account.Name),
                    new Claim(ClaimTypes.Email, account.Email),
                    new Claim("customer_id", "google-" + Accounts.GoogleSubjectDigest(subject)),
                    new Claim("provider", "google"),
                    new Claim("local_admin", account.IsAdmin ? "true" : "false"),
                    new Claim("email_verified", "true")
                ]);
                return;
            };
            o.Events.OnRemoteFailure = c => { c.HandleResponse(); c.Response.Redirect("/login?error=google"); return Task.CompletedTask; };
        });
    }
    public static void UseStoreAuthentication(this WebApplication app)
    {
        var publicUrl = new Uri(app.Configuration["Frontend:Url"] ?? "http://localhost:3000");
        app.Use(async (c, next) =>
        {
            // Trust configuration for OAuth URLs, never forwarded client headers.
            if (c.Request.Path.StartsWithSegments("/api/auth")) { c.Request.Scheme = publicUrl.Scheme; c.Request.Host = new HostString(publicUrl.Host, publicUrl.Port); }
            await next(c);
        });
        // Read the application cookie before limiting, including requests handled by OAuth.
        // Authentication handlers cache the result for the remainder of the request.
        app.Use(async (c, next) =>
        {
            var session = await c.AuthenticateAsync();
            if (session.Succeeded) c.User = session.Principal!;
            await next(c);
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.MapGet("/api/auth/session", (HttpContext c) => Results.Json(new { googleEnabled = Configured(app.Configuration), user = c.User.Identity?.IsAuthenticated == true ? new { customerId = c.User.FindFirstValue("customer_id"), name = c.User.FindFirstValue(ClaimTypes.Name), email = c.User.FindFirstValue(ClaimTypes.Email), isAdmin = IsAdministrator(c.User, app.Configuration), provider = c.User.FindFirstValue("provider") ?? "google" } : null }));
        app.MapGet("/api/auth/google", (string? returnUrl) =>
        {
            if (!Configured(app.Configuration)) return Results.Redirect("/login?error=configuration");
            var safe = returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.Contains('\\') ? returnUrl : "/";
            return Results.Challenge(new AuthenticationProperties { RedirectUri = safe }, [GoogleDefaults.AuthenticationScheme]);
        });
        app.MapPost("/api/auth/logout", async (HttpContext c) => { await c.SignOutAsync(Scheme); return Results.NoContent(); });
        app.Use(async (c, next) =>
        {
            var path = c.Request.Path.Value ?? "";
            var webhook = path == "/api/payments/webhooks/stripe";
            if (!HttpMethods.IsGet(c.Request.Method) && !HttpMethods.IsHead(c.Request.Method) && !webhook && path.StartsWith("/api/", StringComparison.Ordinal) && c.Request.Headers.Origin.ToString() != publicUrl.GetLeftPart(UriPartial.Authority))
            { await Reject(c, 403, "The request must originate from the store."); return; }
            if (c.Request.Path.StartsWithSegments("/api/admin"))
            {
                if (c.User.Identity?.IsAuthenticated != true) { await Reject(c, 401, "Sign in to access store administration."); return; }
                if (!IsAdministrator(c.User, app.Configuration)) { await Reject(c, 403, "This account does not have administrator access."); return; }
                await next(c); return;
            }
            var protectedPath = c.Request.Path.StartsWithSegments("/api/basket") || c.Request.Path.StartsWithSegments("/api/orders") || (c.Request.Path.StartsWithSegments("/api/payments") && !webhook);
            if (!protectedPath) { await next(c); return; }
            var customer = c.User.FindFirstValue("customer_id");
            if (c.User.Identity?.IsAuthenticated != true || customer == null) { await Reject(c, 401, "Sign in to access your bag and orders."); return; }
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts[1] == "basket")
            {
                if (parts.Length < 3 || Uri.UnescapeDataString(parts[2]) != customer) { await Reject(c, 403, "This bag belongs to another account."); return; }
            }
            else if (parts[1] == "orders" && parts.Length == 2 && HttpMethods.IsPost(c.Request.Method))
            {
                using var body = await ReadBody(c); if (body == null) return;
                if (!body.RootElement.TryGetProperty("customerId", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != customer) { await Reject(c, 403, "The order must belong to your account."); return; }
            }
            else
            {
                string? orderId = parts.Length >= 3 ? parts[2] : null;
                if (orderId == "checkout-session") { using var body = await ReadBody(c); if (body == null) return; orderId = body.RootElement.TryGetProperty("orderId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null; }
                if (!Guid.TryParse(orderId, out var orderGuid)) { await Reject(c, 400, "A valid order ID is required."); return; }
                var http = c.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("ownership");
                var ordering = app.Configuration["Services:Ordering"] ?? "http://localhost:5103";
                try
                {
                    using var response = await http.GetAsync($"{ordering}/api/orders/{orderGuid}", c.RequestAborted);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound) { await Reject(c, 404, "Order not found."); return; }
                    if (!response.IsSuccessStatusCode) { await Reject(c, 503, "Order verification is temporarily unavailable."); return; }
                    using var order = JsonDocument.Parse(await response.Content.ReadAsStringAsync(c.RequestAborted));
                    if (!order.RootElement.TryGetProperty("customerId", out var owner) || owner.GetString() != customer) { await Reject(c, 404, "Order not found."); return; }
                }
                catch (HttpRequestException) { await Reject(c, 503, "Order verification is temporarily unavailable."); return; }
                catch (TaskCanceledException) when (!c.RequestAborted.IsCancellationRequested) { await Reject(c, 503, "Order verification is temporarily unavailable."); return; }
            }
            await next(c);
        });
    }
    private static async Task<JsonDocument?> ReadBody(HttpContext c)
    {
        c.Request.EnableBuffering();
        try { return await JsonDocument.ParseAsync(c.Request.Body, cancellationToken: c.RequestAborted); }
        catch (JsonException) { await Reject(c, 400, "Invalid JSON request."); return null; }
        finally { c.Request.Body.Position = 0; }
    }
    private static Task Reject(HttpContext c, int status, string detail) { c.Response.StatusCode = status; return c.Response.WriteAsJsonAsync(new { detail }); }
}
