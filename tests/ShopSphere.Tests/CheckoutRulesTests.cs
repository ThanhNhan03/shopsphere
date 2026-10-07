using ShopSphere.Inventory.Domain;
using ShopSphere.Ordering.Domain;
using ShopSphere.Payment.Domain;
using ShopSphere.SharedKernel;
using Xunit;
using PaymentEntity = ShopSphere.Payment.Domain.Payment;

namespace ShopSphere.Tests;
public sealed class CheckoutRulesTests
{
    [Fact] public void OrderUsesServerPricedItems()
    {
        var order = Order.Create(Guid.NewGuid(), "customer", "Demo Buyer", "buyer@example.com",
            [new() { ProductId = Guid.NewGuid(), Name = "SSD", UnitPrice = 109m, Quantity = 2 }]);
        Assert.Equal(218m, order.TotalAmount);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }
    [Fact] public void EmptyOrderIsRejected() =>
        Assert.Throws<ApiException>(() => Order.Create(Guid.NewGuid(), "customer", "Demo", "buyer@example.com", []));
    [Fact] public void InsufficientInventoryDoesNotChangeStock()
    {
        var stock = new Stock { AvailableQuantity = 2 };
        Assert.False(stock.Reserve(3));
        Assert.Equal(2, stock.AvailableQuantity);
        Assert.Equal(0, stock.ReservedQuantity);
    }
    [Fact] public void ReservationReleaseRestoresAvailability()
    {
        var stock = new Stock { AvailableQuantity = 20 };
        Assert.True(stock.Reserve(3));
        Assert.Equal(17, stock.AvailableQuantity);
        stock.Release(3);
        Assert.Equal(20, stock.AvailableQuantity);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Throws<ApiException>(() => stock.Release(3));
    }
    [Fact] public void ConfirmationCommitsStockWithoutRestoringIt()
    {
        var stock = new Stock { AvailableQuantity = 20 };
        stock.Reserve(3);
        stock.Commit(3);
        Assert.Equal(17, stock.AvailableQuantity);
        Assert.Equal(0, stock.ReservedQuantity);
    }
    [Theory] [InlineData(true, PaymentStatus.Completed)] [InlineData(false, PaymentStatus.Failed)]
    public void DuplicateSettlementDoesNotProduceAnotherTransition(bool paid, PaymentStatus status)
    {
        var payment = new PaymentEntity { Status = PaymentStatus.Processing };
        Assert.True(payment.Settle(paid));
        Assert.False(payment.Settle(paid));
        Assert.False(payment.Settle(!paid));
        Assert.Equal(status, payment.Status);
    }
    [Fact] public void LateReservationCannotReopenConfirmedOrder()
    {
        var order = new Order();
        Assert.True(order.Confirm());
        Assert.False(order.Reserve());
        Assert.False(order.Cancel("late failure"));
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }
    [Fact] public void CancelledOrderCannotConfirm()
    {
        var order = new Order();
        Assert.True(order.Cancel("Payment failed"));
        Assert.False(order.Cancel("duplicate"));
        Assert.False(order.Confirm());
        Assert.False(order.Reserve());
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }
    [Theory] [InlineData(109, 10900)] [InlineData(1.99, 199)]
    public void StripeMinorUnitsAreExact(decimal amount, long expected) => Assert.Equal(expected, PaymentEntity.MinorUnits(amount));
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(1.999)]
    public void InvalidPaymentAmountsAreRejected(decimal amount) =>
        Assert.Throws<ApiException>(() => PaymentEntity.MinorUnits(amount));
}
