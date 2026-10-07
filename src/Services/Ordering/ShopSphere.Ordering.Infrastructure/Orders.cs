using System.Net.Http.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Contracts;
using ShopSphere.Ordering.Application;
using ShopSphere.Ordering.Domain;
using ShopSphere.SharedKernel;
namespace ShopSphere.Ordering.Infrastructure;
public sealed class Orders(OrderingDb db, HttpClient baskets, IPublishEndpoint publish) : IOrders
{
    public Task<Order?> Find(Guid id, CancellationToken ct) => db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);
    public async Task<Order> Checkout(CheckoutRequest request, CancellationToken ct)
    {
        Guard.Require(request.CheckoutId != Guid.Empty, "A checkout identifier is required.");
        Guard.Require(!string.IsNullOrWhiteSpace(request.CustomerId) &&
            System.Text.RegularExpressions.Regex.IsMatch(request.CustomerId, "^[a-zA-Z0-9-]{1,100}$"), "Invalid customer identifier.");
        // A database advisory lock makes concurrent retries of the same checkout serialize.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({request.CheckoutId.ToString()}))", ct);
        var existing = await Find(request.CheckoutId, ct);
        if (existing is not null)
        {
            Guard.Require(existing.CustomerId == request.CustomerId && existing.CustomerName == request.CustomerName?.Trim() &&
                existing.Email == request.Email, "Checkout identifier has already been used for a different request.");
            return existing;
        }
        var response = await baskets.GetAsync($"/api/basket/{Uri.EscapeDataString(request.CustomerId)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest) throw new ApiException(400, "Invalid customer identifier.");
        response.EnsureSuccessStatusCode();
        var basket = await response.Content.ReadFromJsonAsync<BasketSnapshot>(ct) ?? throw new ApiException(409, "Basket unavailable.");
        var order = Order.Create(request.CheckoutId, request.CustomerId, request.CustomerName, request.Email,
            basket.Items.Select(i => new OrderItem { ProductId = i.ProductId, Name = i.Name, UnitPrice = i.UnitPrice, Quantity = i.Quantity }).ToList());
        db.Orders.Add(order);
        await publish.Publish(new OrderCreatedIntegrationEvent(Guid.NewGuid(), order.Id, DateTimeOffset.UtcNow,
            order.Id, order.CustomerId, order.Email, order.TotalAmount,
            order.Items.Select(i => new OrderLine(i.ProductId, i.Name, i.UnitPrice, i.Quantity)).ToArray()), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return order;
    }
    private record BasketSnapshot(BasketLine[] Items);
    private record BasketLine(Guid ProductId, string Name, decimal UnitPrice, int Quantity);
}
