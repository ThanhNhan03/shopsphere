namespace ShopSphere.Inventory.Application;
public record StockView(Guid ProductId, int AvailableQuantity, int ReservedQuantity);
public interface IInventory
{
    Task<StockView?> Find(Guid product, CancellationToken ct);
}
