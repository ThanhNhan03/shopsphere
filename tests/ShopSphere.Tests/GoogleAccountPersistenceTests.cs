using Microsoft.EntityFrameworkCore;
using ShopSphere.Gateway;
using Xunit;

namespace ShopSphere.Tests;

public sealed class GoogleAccountPersistenceTests
{
    [Fact]
    public void GoogleSubjectIsPersistedAsStableDigestRatherThanRawIdentifier()
    {
        const string subject = "google-subject-123";
        var digest = Accounts.GoogleSubjectDigest(subject);

        Assert.Equal(64, digest.Length);
        Assert.Equal(digest, Accounts.GoogleSubjectDigest(subject));
        Assert.DoesNotContain(subject, digest, StringComparison.Ordinal);
        Assert.Equal(digest.ToLowerInvariant(), digest);
    }

    [Fact]
    public void GoogleIdentityModelHasUniqueNullableSubjectAndCurrentMigration()
    {
        using var db = new AccountsDb(new DbContextOptionsBuilder<AccountsDb>()
            .UseNpgsql("Host=localhost;Database=identity_validation;Username=test;Password=test").Options);
        var entity = db.Model.FindEntityType(typeof(Account))!;
        var index = entity.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Account.GoogleSubjectHash));

        Assert.True(index.IsUnique);
        Assert.Contains("IS NOT NULL", index.GetFilter(), StringComparison.OrdinalIgnoreCase);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
