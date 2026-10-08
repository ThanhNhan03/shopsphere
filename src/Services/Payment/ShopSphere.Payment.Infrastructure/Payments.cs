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
        if (Mode == "Demo" && payment.StripeSessionId is not null && !payment.StripeSessionId.StartsWith("demo_", StringComparison.Ordinal))
            throw new ApiException(409, "This order has a Stripe session. Resume it with Stripe payment mode enabled.");
        var stripeKey = Mode == "Stripe" ? StripeSecretKey() : null;
        if (payment.StripeSessionId is not null && stripeKey is not null && payment.IsLive != IsLiveKey(stripeKey))
            throw new ApiException(409, "This order's Stripe session belongs to a different Stripe environment.");
        if (payment.CanReuseCheckout(Mode)) return View(payment);
        if (Mode == "Demo")
        {
            payment.CheckoutUrl = $"{config["Frontend:Url"] ?? "http://localhost:3000"}/orders/{orderId}";
            payment.StripeSessionId = $"demo_{orderId}";
        }
        else
        {
            var key = stripeKey!;
            Session session;
            try
            {
                session = await new SessionService(new StripeClient(key)).CreateAsync(new SessionCreateOptions
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
            }
            catch (StripeException)
            {
                // Provider error messages can contain credential fragments; keep them out of responses and logs.
                throw new ApiException(503, "Stripe Checkout could not be opened. Check the test configuration and try again.");
            }
            payment.StripeSessionId = session.Id;
            payment.CheckoutUrl = session.Url;
            payment.IsLive = IsLiveKey(key);
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
        var stripeKey = StripeSecretKey();
        Guard.Require(session.Id == payment.StripeSessionId && session.Currency == payment.Currency &&
            session.AmountTotal == PaymentEntity.MinorUnits(payment.Amount) && stripeEvent.Livemode == payment.IsLive &&
            payment.IsLive == IsLiveKey(stripeKey), "Webhook does not match the expected Stripe payment environment or amount.");
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
        if (payment.StripeSessionId is not null && !payment.StripeSessionId.StartsWith("demo_", StringComparison.Ordinal))
            throw new ApiException(409, "A Stripe payment cannot be settled with Demo controls.");
        await Settle(payment, paid, "Demo payment declined.", ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return View(payment);
    }
    public async Task<IReadOnlyList<PaymentOperationView>> RecentOperations(CancellationToken ct)
    {
        var payments = await db.Payments.AsNoTracking().OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync(ct);
        var orderIds = payments.Select(p => p.OrderId).ToArray();
        var reconciliations = await db.Reconciliations.AsNoTracking().Where(r => orderIds.Contains(r.OrderId))
            .OrderByDescending(r => r.ReconciledAt).ToListAsync(ct);
        return payments.Select(payment =>
        {
            var last = reconciliations.FirstOrDefault(r => r.OrderId == payment.OrderId);
            var environment = payment.StripeSessionId?.StartsWith("demo_", StringComparison.Ordinal) == true
                ? "Demo" : payment.StripeSessionId is null ? "Not started" : payment.IsLive ? "Live" : "Test";
            return new PaymentOperationView(payment.OrderId, payment.Status, payment.Amount, payment.Currency, environment,
                Mode == "Stripe" && payment.StripeSessionId?.StartsWith("cs_", StringComparison.Ordinal) == true,
                payment.CreatedAt, payment.CompletedAt, last?.ReconciledAt, last?.Outcome, last?.ErrorType);
        }).ToArray();
    }
    public async Task<PaymentReconciliationView> Reconcile(Guid orderId, string actor, CancellationToken ct)
    {
        if (Mode != "Stripe") throw new ApiException(409, "Switch Payment to Stripe mode before reconciling provider sessions.");
        actor = string.IsNullOrWhiteSpace(actor) ? throw new ApiException(401, "Administrator identity is required.") : actor.Trim();
        var key = StripeSecretKey();
        var existing = await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.OrderId == orderId, ct)
            ?? throw new ApiException(404, "Payment not found.");
        if (existing.StripeSessionId is null || !existing.StripeSessionId.StartsWith("cs_", StringComparison.Ordinal))
            throw new ApiException(409, "This order has no Stripe Checkout Session to reconcile.");

        Session session;
        try
        {
            session = await new SessionService(new StripeClient(key)).GetAsync(existing.StripeSessionId, cancellationToken: ct);
        }
        catch (StripeException error)
        {
            await RecordLookupFailure(orderId, actor, error.GetType().Name, ct);
            throw new ApiException(503, "Stripe could not be reached. The failed lookup was recorded; try again later.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var lockKey = $"reconcile-{orderId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", ct);
        var payment = await Lock(orderId, ct);
        var matches = session.Id == payment.StripeSessionId && session.ClientReferenceId == orderId.ToString() &&
            session.Currency == payment.Currency && session.AmountTotal == PaymentEntity.MinorUnits(payment.Amount) &&
            session.Livemode == payment.IsLive && payment.IsLive == IsLiveKey(key);
        var outcome = "NoChange";
        if (!matches)
        {
            outcome = "ProviderDataMismatch";
        }
        else if (session.Status == "complete" && session.PaymentStatus == "paid")
        {
            if (payment.Status is PaymentStatus.Pending or PaymentStatus.Processing)
            {
                payment.StripePaymentIntentId = session.PaymentIntentId;
                await Settle(payment, true, "Stripe operations reconciliation", ct);
                outcome = "ConfirmedFromStripe";
            }
            else outcome = payment.Status == PaymentStatus.Completed ? "AlreadyConfirmed" : "ManualReviewRequired";
        }
        else if (session.Status == "expired")
        {
            if (payment.Status is PaymentStatus.Pending or PaymentStatus.Processing)
            {
                await Settle(payment, false, "Stripe Checkout expired", ct);
                outcome = "ExpiredAndCompensated";
            }
            else outcome = payment.Status is PaymentStatus.Failed or PaymentStatus.Cancelled ? "AlreadyCompensated" : "ManualReviewRequired";
        }

        var record = new PaymentReconciliation
        {
            OrderId = orderId, Actor = actor, ProviderStatus = session.Status ?? "unknown",
            ProviderPaymentStatus = session.PaymentStatus ?? "unknown", Outcome = outcome
        };
        db.Reconciliations.Add(record);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new PaymentReconciliationView(orderId, record.ProviderStatus, record.ProviderPaymentStatus, record.Outcome, record.ReconciledAt);
    }
    private async Task RecordLookupFailure(Guid orderId, string actor, string errorType, CancellationToken ct)
    {
        db.Reconciliations.Add(new PaymentReconciliation
        {
            OrderId = orderId, Actor = actor, Outcome = "ProviderLookupFailed", ErrorType = errorType
        });
        await db.SaveChangesAsync(ct);
    }
    private string StripeSecretKey()
    {
        var key = config["Stripe:SecretKey"];
        if (key is null || !(key.StartsWith("sk_test_", StringComparison.Ordinal) || key.StartsWith("sk_live_", StringComparison.Ordinal)))
            throw new ApiException(503, "Configure a Stripe secret key before starting payment operations.");
        return key;
    }
    private static bool IsLiveKey(string key) => key.StartsWith("sk_live_", StringComparison.Ordinal);
    private async Task Settle(PaymentEntity payment, bool paid, string reason, CancellationToken ct)
    {
        if (!payment.Settle(paid)) return;
        if (paid) await publish.Publish(new PaymentCompletedIntegrationEvent(Guid.NewGuid(), payment.OrderId, DateTimeOffset.UtcNow, payment.OrderId), ct);
        else await publish.Publish(new PaymentFailedIntegrationEvent(Guid.NewGuid(), payment.OrderId, DateTimeOffset.UtcNow, payment.OrderId, reason), ct);
    }
}
