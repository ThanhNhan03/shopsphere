namespace ShopSphere.Basket.Application;
public record BasketItem(Guid ProductId, string Name, decimal UnitPrice, int Quantity, string ImageUrl, bool IsAvailable = true);
public record Basket(string CustomerId, BasketItem[] Items)
{
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
}
public record AddItemRequest(Guid ProductId, int Quantity);
public record QuantityRequest(int Quantity);
public interface IBaskets
{
    Task<Basket> Get(string customer, CancellationToken ct);
    Task<Basket> Add(string customer, AddItemRequest request, CancellationToken ct);
    Task<Basket> Update(string customer, Guid product, int quantity, CancellationToken ct);
    Task Remove(string customer, Guid? product);
}
