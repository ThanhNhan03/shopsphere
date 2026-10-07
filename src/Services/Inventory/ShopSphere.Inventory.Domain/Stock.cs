using ShopSphere.SharedKernel;
namespace ShopSphere.Inventory.Domain;
public sealed class Stock
{
    public Guid ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
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
