using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ShopSphere.Payment.Infrastructure;
public sealed class PaymentDbFactory : IDesignTimeDbContextFactory<PaymentDb>
{
    public PaymentDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PaymentDb>()
        .UseNpgsql("Host=localhost;Port=6543;Database=payment_db;Username=shopsphere;Password=design-time-only")
        .Options);
}
