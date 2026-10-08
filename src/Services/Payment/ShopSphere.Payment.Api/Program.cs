using Microsoft.EntityFrameworkCore;
using ShopSphere.Messaging;
using ShopSphere.Payment.Application;
using ShopSphere.Payment.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.AddDefaults("payment");
var mode = builder.Configuration["Payment:Mode"] ?? "Stripe";
if (mode is not ("Demo" or "Stripe")) throw new InvalidOperationException("Payment:Mode must be Demo or Stripe.");
builder.Services.AddDbContext<PaymentDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<IPayments, Payments>();
builder.Services.AddHostedService<UnstartedPaymentExpiry>();
builder.Services.AddServiceBus<PaymentDb>(builder.Configuration, "payment", typeof(PaymentReservedConsumer).Assembly);
var app = builder.Build();
app.UseDefaults();
app.MapGet("/api/payments/{orderId:guid}", async (Guid orderId, IPayments payments, CancellationToken ct) =>
    await payments.Find(orderId, ct) is { } payment ? Results.Ok(payment) : Results.NotFound());
app.MapPost("/api/payments/checkout-session", (CheckoutSessionRequest request, IPayments payments, CancellationToken ct) => payments.Checkout(request.OrderId, ct));
app.MapPost("/api/payments/{orderId:guid}/simulate", (Guid orderId, SimulateRequest request, IPayments payments, CancellationToken ct) =>
    payments.Simulate(orderId, request.Paid, ct));
app.MapGet("/api/admin/payments", async (IPayments payments, CancellationToken ct) => Results.Ok(await payments.RecentOperations(ct)));
app.MapPost("/api/admin/payments/{orderId:guid}/reconcile", async (Guid orderId, HttpRequest request, IPayments payments, CancellationToken ct) =>
{
    var actor = request.Headers["X-Admin-Email"].ToString();
    if (string.IsNullOrWhiteSpace(actor)) return Results.Unauthorized();
    return Results.Ok(await payments.Reconcile(orderId, actor, ct));
});
app.MapPost("/api/payments/webhooks/stripe", async (HttpRequest request, IPayments payments, CancellationToken ct) =>
{
    using var reader = new StreamReader(request.Body);
    await payments.ProcessWebhook(await reader.ReadToEndAsync(ct), request.Headers["Stripe-Signature"].ToString(), ct);
    return Results.Ok();
});
await app.MigrateDatabase<PaymentDb>();
await app.RunAsync();
