namespace ShopSphere.Catalog.Domain;

// A query projection; Inventory owns stock and reservations.
public sealed class ProductAvailability
{
    public Guid ProductId { get; set; }
    public int AvailableQuantity { get; set; }
    public long Version { get; set; }
}
