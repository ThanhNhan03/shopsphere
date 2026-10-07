using Microsoft.EntityFrameworkCore;
using ShopSphere.Inventory.Domain;
using ShopSphere.SharedKernel;
namespace ShopSphere.Inventory.Infrastructure;
public record AdjustmentWrite(int Delta, int ExpectedAvailable, string Reason);
public sealed class InventoryManagement(InventoryDb db, HttpClient catalog)
{
    public Task<List<Stock>> List(CancellationToken ct) => db.Stocks.AsNoTracking().ToListAsync(ct);
    public Task<List<StockAdjustment>> History(Guid id, CancellationToken ct) => db.Adjustments.AsNoTracking().Where(a => a.ProductId == id).OrderByDescending(a => a.CreatedAt).Take(30).ToListAsync(ct);
    public async Task<Stock> Adjust(Guid id, AdjustmentWrite input, string actor, CancellationToken ct)
    {
        Guard.Require(!string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Trim().Length <= 250, "An adjustment reason is required (max 250 characters).");
        using var product = await catalog.GetAsync($"/api/admin/products/{id}", ct);
        if (product.StatusCode == System.Net.HttpStatusCode.NotFound) throw new ApiException(404, "Product not found.");
        if (!product.IsSuccessStatusCode) throw new ApiException(503, "The catalog is temporarily unavailable.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({id.ToString()}))", ct);
        var stock = await db.Stocks.FromSqlInterpolated($"SELECT * FROM \"Stocks\" WHERE \"ProductId\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (stock is null) { stock = new Stock { ProductId = id }; db.Stocks.Add(stock); }
        if (stock.AvailableQuantity != input.ExpectedAvailable) throw new ApiException(409, "Stock changed while you were editing. Refresh and try again.");
        stock.Adjust(input.Delta);
        db.Adjustments.Add(new StockAdjustment { ProductId = id, Delta = input.Delta, AvailableAfter = stock.AvailableQuantity, Reason = input.Reason.Trim(), PerformedBy = actor });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return stock;
    }
}
