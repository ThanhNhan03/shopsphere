using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopSphere.Contracts;
using ShopSphere.Payment.Domain;
namespace ShopSphere.Payment.Infrastructure;
public sealed class UnstartedPaymentExpiry(IServiceScopeFactory scopes, ILogger<UnstartedPaymentExpiry> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PaymentDb>();
                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
                var payments = await db.Payments.FromSqlInterpolated($"""
                    SELECT * FROM "Payments" WHERE "Status" = 0 AND "CreatedAt" < {cutoff}
                    FOR UPDATE SKIP LOCKED
                    """).ToListAsync(stoppingToken);
                foreach (var payment in payments)
                {
                    payment.Settle(false);
                    await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(
                        new PaymentFailedIntegrationEvent(Guid.NewGuid(), payment.OrderId, DateTimeOffset.UtcNow, payment.OrderId, "Checkout was not started within 30 minutes."), stoppingToken);
                }
                await db.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested) { log.LogError(error, "Could not expire abandoned checkouts"); }
        }
    }
}
