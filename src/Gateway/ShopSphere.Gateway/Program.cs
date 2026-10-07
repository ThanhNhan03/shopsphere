using ShopSphere.Messaging;
using ShopSphere.Gateway;
using Yarp.ReverseProxy.Configuration;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("gateway");
builder.AddStoreAuthentication();
builder.AddStoreRateLimiting();
var services = new[] { ("Catalog", "products"), ("Catalog", "categories"), ("Basket", "basket"),
    ("Ordering", "orders"), ("Inventory", "inventory"), ("Payment", "payments") };
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
builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);
var app = builder.Build();
app.UseDefaults();
app.UseStoreAuthentication();
app.MapReverseProxy();
await app.RunAsync();
public partial class Program { }
