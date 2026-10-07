using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShopSphere.Basket.Application;
using ShopSphere.SharedKernel;

namespace ShopSphere.Basket.Infrastructure;

public sealed class InventoryAvailability(HttpClient http) : IStockAvailability
{
    public async Task<int> Available(Guid productId, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync($"/api/inventory/{productId}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return 0;
            if (!response.IsSuccessStatusCode) throw Unavailable();
            var stock = await response.Content.ReadFromJsonAsync<StockResponse>(ct);
            if (stock is null || stock.ProductId != productId || stock.AvailableQuantity is null or < 0) throw Unavailable();
            return stock.AvailableQuantity.Value;
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
    }

    private static ApiException Unavailable() => new(503, "Stock availability cannot be verified. Please try again shortly.");
    private sealed record StockResponse(Guid ProductId, int? AvailableQuantity);
}
