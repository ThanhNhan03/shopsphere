using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ShopSphere.Basket.Application;
using ShopSphere.SharedKernel;
using StackExchange.Redis;
using BasketView = ShopSphere.Basket.Application.Basket;

namespace ShopSphere.Basket.Infrastructure;
public sealed class RedisBaskets(IConnectionMultiplexer redis, HttpClient catalog, IStockAvailability inventory) : IBaskets
{
    private IDatabase Db => redis.GetDatabase();
    private static string Key(string customer)
    {
        Guard.Require(Regex.IsMatch(customer, "^[a-zA-Z0-9-]{1,100}$"), "Invalid customer identifier.");
        return $"basket:{customer}";
    }
    public async Task<BasketView> Get(string customer, CancellationToken ct)
    {
        var entries = await Db.HashGetAllAsync(Key(customer));
        var items = new List<BasketItem>();
        foreach (var entry in entries.OrderBy(e => e.Name.ToString()))
        {
            var product = await catalog.GetFromJsonAsync<CatalogProduct>($"/internal/products/{entry.Name}", ct)
                ?? throw new ApiException(409, "A basket product is no longer available.");
            var available = product.IsActive ? await inventory.Available(product.Id, ct) : 0;
            items.Add(new(product.Id, product.Name, product.Price, (int)entry.Value, product.ImageUrl, available, product.IsActive));
        }
        return new(customer, items.ToArray());
    }
    public async Task<BasketView> Add(string customer, AddItemRequest request, CancellationToken ct)
    {
        Guard.Require(request.Quantity is >= 1 and <= 99, "Quantity must be between 1 and 99.");
        var key = Key(customer);
        using var response = await catalog.GetAsync($"/api/products/{request.ProductId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) throw new ApiException(404, "Product not found.");
        response.EnsureSuccessStatusCode();
        var available = await inventory.Available(request.ProductId, ct);
        var updated = (int)await Db.ScriptEvaluateAsync("""
            local q = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0') + tonumber(ARGV[2])
            if q > 99 then return -2 end
            if q > tonumber(ARGV[3]) then return -1 end
            redis.call('HSET', KEYS[1], ARGV[1], q)
            redis.call('EXPIRE', KEYS[1], 604800)
            return 1
            """, [key], [request.ProductId.ToString(), request.Quantity, available]);
        if (updated == -1) throw StockConflict(available, adding: true);
        Guard.Require(updated == 1, "A basket can contain at most 99 of each product.");
        return await Get(customer, ct);
    }
    public async Task<BasketView> Update(string customer, Guid product, int quantity, CancellationToken ct)
    {
        Guard.Require(quantity is >= 1 and <= 99, "Quantity must be between 1 and 99.");
        var available = await inventory.Available(product, ct);
        var updated = (int)await Db.ScriptEvaluateAsync("""
            local current = tonumber(redis.call('HGET', KEYS[1], ARGV[1]))
            if not current then return 0 end
            local requested = tonumber(ARGV[2])
            -- Let stale quantities be reduced; increases must fit current stock.
            if requested > tonumber(ARGV[3]) and requested >= current then return -1 end
            redis.call('HSET', KEYS[1], ARGV[1], ARGV[2])
            redis.call('EXPIRE', KEYS[1], 604800)
            return 1
            """, [Key(customer)], [product.ToString(), quantity, available]);
        if (updated == 0) throw new ApiException(404, "Basket item not found.");
        if (updated == -1) throw StockConflict(available);
        return await Get(customer, ct);
    }
    public async Task Remove(string customer, Guid? product)
    {
        if (product is { } id) await Db.HashDeleteAsync(Key(customer), id.ToString());
        else await Db.KeyDeleteAsync(Key(customer));
    }
    private record CatalogProduct(Guid Id, string Name, decimal Price, string ImageUrl, bool IsActive);
    private static ApiException StockConflict(int available, bool adding = false) => new(409, available == 0
        ? adding ? "This product is currently out of stock and cannot be added to your bag."
            : "This product is out of stock. Remove it from your bag before checking out."
        : adding ? $"Only {available} are currently available, including any already in your bag. Open your bag to review the quantity."
            : $"Only {available} of this product are currently available. Reduce the quantity before checking out.");
}
