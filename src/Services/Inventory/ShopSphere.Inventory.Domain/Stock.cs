using ShopSphere.SharedKernel;
namespace ShopSphere.Inventory.Domain;
public sealed class Stock
{
    public Guid ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public void Adjust(int delta)
    {
        Guard.Require(delta != 0 && (long)AvailableQuantity + delta is >= 0 and <= 1_000_000, "The adjustment must leave available stock between 0 and 1,000,000.");
        AvailableQuantity += delta;
    }
    public bool Reserve(int quantity)
    {
        Guard.Require(quantity > 0, "Reservation quantity must be positive.");
        if (AvailableQuantity < quantity) return false;
        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
        return true;
    }
    public void Release(int quantity)
    {
        Guard.Require(quantity > 0 && quantity <= ReservedQuantity, "Invalid inventory release.");
        ReservedQuantity -= quantity;
        AvailableQuantity += quantity;
    }
    public void Commit(int quantity)
    {
        Guard.Require(quantity > 0 && quantity <= ReservedQuantity, "Invalid inventory commitment.");
        ReservedQuantity -= quantity;
    }
}
public sealed class StockAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public int Delta { get; set; }
    public int AvailableAfter { get; set; }
    public string Reason { get; set; } = "";
    public string PerformedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Reservation
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "Reserved";
    public List<ReservationItem> Items { get; set; } = [];
}
public sealed class ReservationItem
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
