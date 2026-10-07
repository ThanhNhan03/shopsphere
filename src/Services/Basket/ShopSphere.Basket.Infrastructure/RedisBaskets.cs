using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ShopSphere.Basket.Application;
using ShopSphere.SharedKernel;
using StackExchange.Redis;
using BasketView = ShopSphere.Basket.Application.Basket;

namespace ShopSphere.Basket.Infrastructure;
public sealed class RedisBaskets(IConnectionMultiplexer redis, HttpClient catalog) : IBaskets
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
            items.Add(new(product.Id, product.Name, product.Price, (int)entry.Value, product.ImageUrl, product.IsActive));
        }
        return new(customer, items.ToArray());
    }
    public async Task<BasketView> Add(string customer, AddItemRequest request, CancellationToken ct)
    {
        Guard.Require(request.Quantity is >= 1 and <= 99, "Quantity must be between 1 and 99.");
        var key = Key(customer);
        var response = await catalog.GetAsync($"/api/products/{request.ProductId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) throw new ApiException(404, "Product not found.");
        response.EnsureSuccessStatusCode();
        var updated = (int)await Db.ScriptEvaluateAsync("""
            local q = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0') + tonumber(ARGV[2])
            if q > 99 then return 0 end
            redis.call('HSET', KEYS[1], ARGV[1], q)
            redis.call('EXPIRE', KEYS[1], 604800)
            return 1
            """, [key], [request.ProductId.ToString(), request.Quantity]);
        Guard.Require(updated == 1, "A basket can contain at most 99 of each product.");
        return await Get(customer, ct);
    }
    public async Task<BasketView> Update(string customer, Guid product, int quantity, CancellationToken ct)
    {
        Guard.Require(quantity is >= 1 and <= 99, "Quantity must be between 1 and 99.");
        var updated = (int)await Db.ScriptEvaluateAsync("""
            if redis.call('HEXISTS', KEYS[1], ARGV[1]) == 0 then return 0 end
            redis.call('HSET', KEYS[1], ARGV[1], ARGV[2])
            redis.call('EXPIRE', KEYS[1], 604800)
            return 1
            """, [Key(customer)], [product.ToString(), quantity]);
        if (updated == 0) throw new ApiException(404, "Basket item not found.");
        return await Get(customer, ct);
    }
    public async Task Remove(string customer, Guid? product)
    {
        if (product is { } id) await Db.HashDeleteAsync(Key(customer), id.ToString());
        else await Db.KeyDeleteAsync(Key(customer));
    }
    private record CatalogProduct(Guid Id, string Name, decimal Price, string ImageUrl, bool IsActive);
}
