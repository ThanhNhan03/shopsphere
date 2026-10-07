using ShopSphere.Ordering.Domain;
namespace ShopSphere.Ordering.Application;
public record CheckoutRequest(Guid CheckoutId, string CustomerId, string CustomerName, string Email);
public interface IOrders
{
    Task<Order> Checkout(CheckoutRequest request, CancellationToken ct);
    Task<Order?> Find(Guid id, CancellationToken ct);
}
