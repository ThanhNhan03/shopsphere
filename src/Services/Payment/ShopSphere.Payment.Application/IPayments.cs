using ShopSphere.Payment.Domain;
namespace ShopSphere.Payment.Application;
public record CheckoutSessionRequest(Guid OrderId);
public record SimulateRequest(bool Paid);
public record PaymentView(Guid OrderId, PaymentStatus Status, decimal Amount, string Currency, string? CheckoutUrl, string Mode);
public record PaymentOperationView(Guid OrderId, PaymentStatus Status, decimal Amount, string Currency, string Environment,
    bool CanReconcile, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, DateTimeOffset? LastReconciledAt,
    string? LastOutcome, string? LastErrorType);
public record PaymentReconciliationView(Guid OrderId, string ProviderStatus, string ProviderPaymentStatus, string Outcome, DateTimeOffset ReconciledAt);
public interface IPayments
{
    Task<PaymentView?> Find(Guid orderId, CancellationToken ct);
    Task<PaymentView> Checkout(Guid orderId, CancellationToken ct);
    Task ProcessWebhook(string payload, string signature, CancellationToken ct);
    Task<PaymentView> Simulate(Guid orderId, bool paid, CancellationToken ct);
    Task<IReadOnlyList<PaymentOperationView>> RecentOperations(CancellationToken ct);
    Task<PaymentReconciliationView> Reconcile(Guid orderId, string actor, CancellationToken ct);
}
