using Microsoft.EntityFrameworkCore;
using ShopSphere.Basket.Application;
using ShopSphere.Catalog.Infrastructure;
using ShopSphere.Inventory.Domain;
using ShopSphere.Inventory.Infrastructure;
using Xunit;
using BasketView = ShopSphere.Basket.Application.Basket;

namespace ShopSphere.Tests;

[Trait("Category", "Unit")]
public sealed class MergeCompatibilityTests
{
    [Theory]
    [InlineData(true, 2, 2, true)]
    [InlineData(true, 3, 2, false)]
    [InlineData(false, 1, 10, false)]
    [InlineData(true, 1, 0, false)]
    public void CheckoutRequiresVisibleProductsAndSufficientStock(bool visible, int quantity, int available, bool expected)
    {
        var basket = new BasketView("customer", [new BasketItem(Guid.NewGuid(), "Product", 10m, quantity, "image", available, visible)]);
        Assert.Equal(expected, basket.CanCheckout);
    }

    [Fact]
    public void AdminAdjustmentAdvancesAvailabilityVersionAndPreservesReservedStock()
    {
        var stock = new Stock { AvailableQuantity = 5, ReservedQuantity = 3, Version = 7 };
        stock.Adjust(-5);
        Assert.Equal(8L, stock.Version);
        Assert.Equal(0, stock.AvailableQuantity);
        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Throws<ShopSphere.SharedKernel.ApiException>(() => stock.Adjust(-1));
        Assert.Equal(8L, stock.Version);
    }

    [Fact]
    public void CatalogMigrationsMatchCombinedAdministrationAndAvailabilityModel()
    {
        using var db = new CatalogDb(new DbContextOptionsBuilder<CatalogDb>()
            .UseNpgsql("Host=localhost;Database=merge_validation;Username=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void InventoryMigrationsMatchCombinedAdjustmentAndVersionModel()
    {
        using var db = new InventoryDb(new DbContextOptionsBuilder<InventoryDb>()
            .UseNpgsql("Host=localhost;Database=merge_validation;Username=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
