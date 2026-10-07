using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShopSphere.Contracts;
using ShopSphere.Payment.Application;
using ShopSphere.Payment.Domain;
using ShopSphere.SharedKernel;
using Stripe;
using Stripe.Checkout;
using PaymentEntity = ShopSphere.Payment.Domain.Payment;
namespace ShopSphere.Payment.Infrastructure;
public sealed class Payments(PaymentDb db, IPublishEndpoint publish, IConfiguration config) : IPayments
{
    private string Mode => config["Payment:Mode"] ?? "Stripe";
    private PaymentView View(PaymentEntity p) => new(p.OrderId, p.Status, p.Amount, p.Currency, p.CheckoutUrl, Mode);
    public async Task<PaymentView?> Find(Guid orderId, CancellationToken ct) =>
        await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.OrderId == orderId, ct) is { } p ? View(p) : null;
    private async Task<PaymentEntity> Lock(Guid id, CancellationToken ct) =>
        await db.Payments.FromSqlInterpolated($"SELECT * FROM \"Payments\" WHERE \"OrderId\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        ?? throw new ApiException(409, "Inventory reservation is still being processed. Try again shortly.");
    public async Task<PaymentView> Checkout(Guid orderId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var payment = await Lock(orderId, ct);
        if (payment.Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Cancelled)
            throw new ApiException(409, "Payment has already finished.");
        if (payment.CheckoutUrl != null) return View(payment);
        if (Mode == "Demo")
        {
            payment.CheckoutUrl = $"{config["Frontend:Url"] ?? "http://localhost:3000"}/orders/{orderId}";
            payment.StripeSessionId = $"demo_{orderId}";
        }
        else
        {
            var key = config["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("sk_test_", StringComparison.Ordinal))
                throw new ApiException(503, "Configure a Stripe test secret key before starting payment.");
            var session = await new SessionService(new StripeClient(key)).CreateAsync(new SessionCreateOptions
            {
                Mode = "payment", CustomerEmail = payment.Email, ClientReferenceId = orderId.ToString(),
                AllowedPaymentMethodTypes = ["card"],
                SuccessUrl = $"{config["Frontend:Url"] ?? "http://localhost:3000"}/payment/success?order_id={orderId}&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{config["Frontend:Url"] ?? "http://localhost:3000"}/orders/{orderId}?cancelled=1",
                ExpiresAt = DateTime.UtcNow.AddMinutes(31),
                Metadata = new() { ["orderId"] = orderId.ToString() },
                LineItems = [new SessionLineItemOptions
                {
                    Quantity = 1, PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = payment.Currency, UnitAmount = PaymentEntity.MinorUnits(payment.Amount),
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = $"ShopSphere order {orderId}" }
                    }
                }]
            }, new RequestOptions { IdempotencyKey = $"checkout-{orderId}" }, ct);
            payment.StripeSessionId = session.Id;
            payment.CheckoutUrl = session.Url;
        }
        payment.Status = PaymentStatus.Processing;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return View(payment);
    }
    public async Task ProcessWebhook(string payload, string signature, CancellationToken ct)
    {
        if (Mode != "Stripe") throw new ApiException(404, "Stripe webhooks are disabled in Demo mode.");
        var secret = config["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) throw new ApiException(503, "Configure the Stripe webhook secret.");
        Stripe.Event stripeEvent;
        try { stripeEvent = EventUtility.ConstructEvent(payload, signature, secret, throwOnApiVersionMismatch: false); }
        catch (Exception error) when (error is StripeException or System.Text.Json.JsonException)
        { throw new ApiException(400, "Invalid Stripe webhook signature or payload."); }
        var supported = stripeEvent.Type is "checkout.session.completed" or "checkout.session.async_payment_succeeded"
            or "checkout.session.async_payment_failed" or "checkout.session.expired";
        if (!supported) return;
        if (stripeEvent.Data.Object is not Session session) throw new ApiException(400, "Expected a Checkout Session.");
        if (!Guid.TryParse(session.ClientReferenceId, out var orderId)) throw new ApiException(400, "Missing order reference.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Provider event identity is persisted in the same transaction as payment and outgoing events.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({stripeEvent.Id}))", ct);
        if (await db.StripeReceipts.AnyAsync(r => r.EventId == stripeEvent.Id, ct)) return;
        var payment = await Lock(orderId, ct);
        Guard.Require(session.Id == payment.StripeSessionId && session.Currency == payment.Currency &&
            session.AmountTotal == PaymentEntity.MinorUnits(payment.Amount) && !stripeEvent.Livemode,
            "Webhook does not match the expected test-mode payment.");
        var paid = stripeEvent.Type is "checkout.session.completed" or "checkout.session.async_payment_succeeded";
        if (paid && session.PaymentStatus != "paid") return;
        db.StripeReceipts.Add(new() { EventId = stripeEvent.Id });
        payment.StripePaymentIntentId = session.PaymentIntentId;
        await Settle(payment, paid, stripeEvent.Type, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task<PaymentView> Simulate(Guid orderId, bool paid, CancellationToken ct)
    {
        if (Mode != "Demo") throw new ApiException(404, "Simulation is available only in Demo mode.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var payment = await Lock(orderId, ct);
        await Settle(payment, paid, "Demo payment declined.", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return View(payment);
    }
    private async Task Settle(PaymentEntity payment, bool paid, string reason, CancellationToken ct)
    {
        if (!payment.Settle(paid)) return;
        if (paid) await publish.Publish(new PaymentCompletedIntegrationEvent(Guid.NewGuid(), payment.OrderId, DateTimeOffset.UtcNow, payment.OrderId), ct);
        else await publish.Publish(new PaymentFailedIntegrationEvent(Guid.NewGuid(), payment.OrderId, DateTimeOffset.UtcNow, payment.OrderId, reason), ct);
    }
}
