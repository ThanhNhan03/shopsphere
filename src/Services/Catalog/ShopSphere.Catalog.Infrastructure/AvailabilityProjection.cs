using System.Net.Http.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShopSphere.Contracts;

namespace ShopSphere.Catalog.Infrastructure;

public sealed class AvailabilityReadiness
{
    private volatile bool ready;
    public bool Ready => ready;
    public void MarkReady() => ready = true;
}

public sealed record AvailabilitySnapshot(Guid ProductId, int AvailableQuantity, long Version);
public sealed record AvailabilitySnapshotPage(AvailabilitySnapshot[] Items, Guid? NextAfter);

public sealed class AvailabilitySnapshotClient(HttpClient http)
{
    public async Task<AvailabilitySnapshotPage> Read(Guid after, CancellationToken ct)
    {
        var page = await http.GetFromJsonAsync<AvailabilitySnapshotPage>(
            $"/internal/availability?after={after}&limit=2000", ct)
            ?? throw new InvalidOperationException("Inventory snapshot is missing.");
        if (page.Items.Length > 2000 || page.Items.Any(s => s.ProductId.CompareTo(after) <= 0 || s.AvailableQuantity < 0 || s.Version < 0)
            || page.Items.Select(s => s.ProductId).Distinct().Count() != page.Items.Length
            || (page.NextAfter is { } next && (page.Items.Length == 0 || next != page.Items[^1].ProductId)))
            throw new InvalidOperationException("Inventory snapshot is invalid.");
        return page;
    }
}

public static class AvailabilityProjection
{
    // Versioned UPSERT prevents delayed/duplicate events or bootstrap pages from restoring stale stock.
    public static Task<int> Apply(CatalogDb db, AvailabilitySnapshot[] items, CancellationToken ct)
    {
        if (items.Length == 0) return Task.FromResult(0);
        return db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "ProductAvailability" ("ProductId", "AvailableQuantity", "Version")
            SELECT * FROM unnest(@ids, @quantities, @versions)
            ON CONFLICT ("ProductId") DO UPDATE SET
                "AvailableQuantity" = EXCLUDED."AvailableQuantity", "Version" = EXCLUDED."Version"
            WHERE "ProductAvailability"."Version" < EXCLUDED."Version"
            """, new object[] {
                new NpgsqlParameter("ids", items.Select(i => i.ProductId).ToArray()),
                new NpgsqlParameter("quantities", items.Select(i => i.AvailableQuantity).ToArray()),
                new NpgsqlParameter("versions", items.Select(i => i.Version).ToArray())
            }, ct);
    }
}

public sealed class AvailabilityChangedConsumer(CatalogDb db) : IConsumer<StockAvailabilityChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<StockAvailabilityChangedIntegrationEvent> context)
    {
        var m = context.Message;
        if (m.AvailableQuantity < 0 || m.Version < 0) throw new InvalidOperationException("Invalid stock event.");
        await AvailabilityProjection.Apply(db, [new(m.ProductId, m.AvailableQuantity, m.Version)], context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class AvailabilitySynchronizer(IServiceScopeFactory scopes, AvailabilityReadiness readiness,
    ILogger<AvailabilitySynchronizer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CatalogDb>();
                var snapshots = scope.ServiceProvider.GetRequiredService<AvailabilitySnapshotClient>();
                var after = Guid.Empty;
                long rows = 0;
                do
                {
                    var page = await snapshots.Read(after, stoppingToken);
                    await AvailabilityProjection.Apply(db, page.Items, stoppingToken);
                    rows += page.Items.Length;
                    if (page.NextAfter is not { } next) break;
                    after = next;
                } while (!stoppingToken.IsCancellationRequested);
                stoppingToken.ThrowIfCancellationRequested();
                readiness.MarkReady();
                log.LogInformation("Availability projection reconciled {Rows} Inventory rows", rows);
                // Covers insert-only sample seeding and missed operational updates; events handle purchases.
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                log.LogWarning(error, "Inventory availability reconciliation failed; retrying");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
