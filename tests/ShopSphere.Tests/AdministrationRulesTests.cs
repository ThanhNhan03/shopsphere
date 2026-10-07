using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using ShopSphere.Catalog.Domain;
using ShopSphere.Catalog.Infrastructure;
using ShopSphere.Gateway;
using ShopSphere.Inventory.Domain;
using ShopSphere.SharedKernel;
using Xunit;

namespace ShopSphere.Tests;
public sealed class AdministrationRulesTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void AdministratorRequiresServerRoleOrVerifiedAllowedEmail(bool localAdmin, bool verified, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, "ADMIN@example.com"),
            new Claim("local_admin", localAdmin ? "true" : "false"), new Claim("email_verified", verified ? "true" : "false") }, "test"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Emails"] = "admin@example.com" }).Build();
        Assert.Equal(expected, StoreAuthentication.IsAdministrator(user, config));
        Assert.False(StoreAuthentication.IsAdministrator(new ClaimsPrincipal(new ClaimsIdentity(user.Claims)), config));
    }
    [Fact] public void PublicRegistrationCannotSetAdministratorRole() => Assert.False(new Account().IsAdmin);
    [Theory] [InlineData("a")] [InlineData("123456789")]
    public void ShortPasswordRejected(string password) => Assert.Throws<ApiException>(() => Accounts.ValidatePassword(password));
    [Fact] public void EmailNormalizedAndDisplayNameSyntaxRejected()
    {
        Assert.Equal("name@example.com", Accounts.NormalizeEmail(" Name@Example.com "));
        Assert.Throws<ApiException>(() => Accounts.NormalizeEmail("Name <name@example.com>"));
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(1.001)]
    public void InvalidProductPriceRejected(double price) => Assert.Throws<ApiException>(() => CatalogManagement.Validate(new("Product", "Description", (decimal)price, "Brand", "Audio", true)));
    [Fact] public void StaleProductVersionRejected() => Assert.Equal(409, Assert.Throws<ApiException>(() => CatalogManagement.CheckVersion(new Product { Version = 2 }, 1)).StatusCode);
    [Fact] public void ImageTypeMustMatchBytes()
    {
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10];
        Assert.Equal("png", ProductImages.Validate(png, "image/png"));
        Assert.Throws<ApiException>(() => ProductImages.Validate(png, "image/jpeg"));
        Assert.Throws<ApiException>(() => ProductImages.Validate("<svg/>"u8.ToArray(), "image/svg+xml"));
        Assert.Throws<ApiException>(() => ProductImages.Validate(new byte[ProductImages.MaxBytes + 1], "image/png"));
    }
    [Fact] public void AdjustmentNeverConsumesReservedUnits()
    {
        var stock = new Stock { AvailableQuantity = 5, ReservedQuantity = 3 };
        stock.Adjust(-5);
        Assert.Equal(0, stock.AvailableQuantity); Assert.Equal(3, stock.ReservedQuantity);
        Assert.Throws<ApiException>(() => stock.Adjust(-1));
        Assert.Throws<ApiException>(() => stock.Adjust(0));
        Assert.Throws<ApiException>(() => stock.Adjust(int.MaxValue));
        Assert.Equal(0, stock.AvailableQuantity); Assert.Equal(3, stock.ReservedQuantity);
    }
}
