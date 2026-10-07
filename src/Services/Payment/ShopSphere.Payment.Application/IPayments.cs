using ShopSphere.Payment.Domain;
namespace ShopSphere.Payment.Application;
public record CheckoutSessionRequest(Guid OrderId);
public record SimulateRequest(bool Paid);
public record PaymentView(Guid OrderId, PaymentStatus Status, decimal Amount, string Currency, string? CheckoutUrl, string Mode);
public interface IPayments
{
    Task<PaymentView?> Find(Guid orderId, CancellationToken ct);
    Task<PaymentView> Checkout(Guid orderId, CancellationToken ct);
    Task ProcessWebhook(string payload, string signature, CancellationToken ct);
    Task<PaymentView> Simulate(Guid orderId, bool paid, CancellationToken ct);
}
