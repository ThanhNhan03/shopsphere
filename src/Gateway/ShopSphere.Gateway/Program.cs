using ShopSphere.Messaging;
using ShopSphere.Gateway;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("gateway");
builder.AddStoreAuthentication();
builder.Services.AddDbContext<AccountsDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<Accounts>();
builder.AddStoreRateLimiting();
var services = new[] { ("Catalog", "products"), ("Catalog", "categories"), ("Basket", "basket"),
    ("Ordering", "orders"), ("Inventory", "inventory"), ("Payment", "payments"), ("Payment", "admin/payments"),
    ("Catalog", "admin/products"), ("Inventory", "admin/inventory"), ("Ordering", "admin/orders"), ("Catalog", "media") };
var routes = services.Select((s, i) => new RouteConfig
{
    RouteId = $"route-{i}", ClusterId = s.Item1.ToLowerInvariant(),
    Match = new RouteMatch { Path = $"/api/{s.Item2}/{{**remainder}}" }
}).ToArray();
var clusters = services.Select(s => s.Item1).Distinct().Select((s, i) => new ClusterConfig
{
    ClusterId = s.ToLowerInvariant(),
    Destinations = new Dictionary<string, DestinationConfig>
    { ["primary"] = new() { Address = builder.Configuration[$"Services:{s}"] ?? $"http://localhost:{5101 + i}" } }
}).ToArray();
builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters).AddTransforms(context =>
    context.AddRequestTransform(c =>
    {
        c.ProxyRequest.Headers.Remove("X-Admin-Email");
        if (StoreAuthentication.IsAdministrator(c.HttpContext.User, builder.Configuration))
            c.ProxyRequest.Headers.TryAddWithoutValidation("X-Admin-Email", c.HttpContext.User.FindFirstValue(ClaimTypes.Email));
        return ValueTask.CompletedTask;
    }));
var app = builder.Build();
app.UseDefaults();
app.UseStoreAuthentication();
app.MapLocalAccounts();
app.MapReverseProxy();
await app.MigrateDatabase<AccountsDb>();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<Accounts>().SeedAdministrator(app.Configuration, CancellationToken.None);
await app.RunAsync();
public partial class Program { }
