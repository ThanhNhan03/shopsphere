using ShopSphere.SharedKernel;
namespace ShopSphere.Payment.Domain;
public enum PaymentStatus { Pending, Processing, Completed, Failed, Cancelled }
public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Email { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string? StripeSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? CheckoutUrl { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public bool Settle(bool paid)
    {
        if (Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Cancelled) return false;
        Status = paid ? PaymentStatus.Completed : PaymentStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        return true;
    }
    public static long MinorUnits(decimal amount)
    {
        Guard.Require(amount > 0 && decimal.Round(amount, 2) == amount, "USD amounts must be positive and have at most two decimal places.");
        return checked((long)(amount * 100));
    }
}
