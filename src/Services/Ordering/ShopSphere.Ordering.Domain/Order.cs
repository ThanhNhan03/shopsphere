using ShopSphere.SharedKernel;
namespace ShopSphere.Ordering.Domain;
public enum OrderStatus { Pending, InventoryReserved, AwaitingPayment, Confirmed, Cancelled }
public sealed class Order
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Email { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? CancellationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<OrderItem> Items { get; set; } = [];
    public bool Reserve()
    {
        if (Status != OrderStatus.Pending) return false;
        Status = OrderStatus.AwaitingPayment;
        return true;
    }
    public bool Confirm()
    {
        if (Status is OrderStatus.Confirmed or OrderStatus.Cancelled) return false;
        Status = OrderStatus.Confirmed;
        return true;
    }
    public bool Cancel(string reason)
    {
        if (Status is OrderStatus.Confirmed or OrderStatus.Cancelled) return false;
        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        return true;
    }
    public static Order Create(Guid id, string customer, string name, string email, List<OrderItem> items)
    {
        Guard.Require(id != Guid.Empty, "A checkout identifier is required.");
        Guard.Require(items.Count > 0 && items.Count <= 100, "Basket must contain between 1 and 100 products.");
        Guard.Require(items.All(i => i.Quantity is >= 1 and <= 99 && i.UnitPrice > 0), "Invalid order items.");
        Guard.Require(!string.IsNullOrWhiteSpace(name) && name.Length <= 100, "Customer name is required (max 100 characters).");
        Guard.Require(System.Net.Mail.MailAddress.TryCreate(email, out _) && email.Length <= 254, "A valid email is required.");
        return new() { Id = id, CustomerId = customer, CustomerName = name.Trim(), Email = email,
            Items = items, TotalAmount = items.Sum(i => i.UnitPrice * i.Quantity) };
    }
}
public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
