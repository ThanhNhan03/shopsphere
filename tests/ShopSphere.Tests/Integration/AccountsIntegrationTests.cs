using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Gateway;
using ShopSphere.SharedKernel;
using Xunit;

namespace ShopSphere.Tests.Integration;

public sealed class AccountsIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RegistrationPersistsNormalizedAccountAndLoginReadsItBack()
    {
        await using var database = await TestDatabase.Create();
        var accounts = new Accounts(database.Context);

        var registered = await accounts.Register(
            new Registration("  Demo Buyer  ", "  BUYER@Example.com  ", "correct-horse-42"),
            CancellationToken.None);
        database.Context.ChangeTracker.Clear();
        var loggedIn = await accounts.Login(
            new SignIn("buyer@example.com", "correct-horse-42"),
            CancellationToken.None);

        Assert.Equal(registered.Id, loggedIn.Id);
        Assert.Equal("Demo Buyer", loggedIn.Name);
        Assert.Equal("buyer@example.com", loggedIn.Email);
        Assert.NotEqual("correct-horse-42", loggedIn.PasswordHash);
        Assert.Single(await database.Context.Accounts.ToListAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseUniqueIndexRejectsDuplicateNormalizedEmail()
    {
        await using var database = await TestDatabase.Create();
        var accounts = new Accounts(database.Context);
        await accounts.Register(
            new Registration("First Buyer", "buyer@example.com", "correct-horse-42"),
            CancellationToken.None);

        database.Context.Accounts.Add(new Account
        {
            Name = "Duplicate Buyer",
            Email = "buyer@example.com",
            PasswordHash = "not-used"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.Context.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WrongPasswordIsRejectedWithoutChangingStoredAccount()
    {
        await using var database = await TestDatabase.Create();
        var accounts = new Accounts(database.Context);
        var registered = await accounts.Register(
            new Registration("Demo Buyer", "buyer@example.com", "correct-horse-42"),
            CancellationToken.None);
        var originalHash = registered.PasswordHash;
        database.Context.ChangeTracker.Clear();

        var error = await Assert.ThrowsAsync<ApiException>(() => accounts.Login(
            new SignIn("buyer@example.com", "wrong-password"),
            CancellationToken.None));
        var stored = await database.Context.Accounts.SingleAsync(CancellationToken.None);

        Assert.Equal(401, error.StatusCode);
        Assert.Equal(originalHash, stored.PasswordHash);
    }

    private sealed class TestDatabase(SqliteConnection connection, AccountsDb context) : IAsyncDisposable
    {
        public AccountsDb Context { get; } = context;

        public static async Task<TestDatabase> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(CancellationToken.None);
            var context = new AccountsDb(new DbContextOptionsBuilder<AccountsDb>()
                .UseSqlite(connection)
                .Options);
            await context.Database.EnsureCreatedAsync(CancellationToken.None);
            return new(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
