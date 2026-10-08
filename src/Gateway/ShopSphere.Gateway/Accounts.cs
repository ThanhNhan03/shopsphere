using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShopSphere.SharedKernel;

namespace ShopSphere.Gateway;
public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? GoogleSubjectHash { get; set; }
    public string? GooglePictureUrl { get; set; }
    public DateTimeOffset? GoogleLastLoginAt { get; set; }
}
public sealed class AccountsDb(DbContextOptions<AccountsDb> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Account>().HasIndex(a => a.Email).IsUnique();
        model.Entity<Account>().Property(a => a.Email).HasMaxLength(254);
        model.Entity<Account>().Property(a => a.Name).HasMaxLength(100);
        model.Entity<Account>().Property(a => a.GoogleSubjectHash).HasMaxLength(64);
        model.Entity<Account>().Property(a => a.GooglePictureUrl).HasMaxLength(2048);
        model.Entity<Account>().HasIndex(a => a.GoogleSubjectHash).IsUnique().HasFilter("\"GoogleSubjectHash\" IS NOT NULL");
    }
}
public sealed class AccountsDbFactory : IDesignTimeDbContextFactory<AccountsDb>
{
    public AccountsDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AccountsDb>().UseNpgsql("Host=localhost;Port=6543;Database=identity_db;Username=shopsphere;Password=design-time-only").Options);
}
public record Registration(string Name, string Email, string Password);
public record SignIn(string Email, string Password);
public sealed class Accounts(AccountsDb db)
{
    private static readonly PasswordHasher<Account> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(new Account(), "timing-only-invalid-account");
    public static string NormalizeEmail(string email)
    {
        var value = email?.Trim() ?? "";
        Guard.Require(value.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(value, out var parsed) && parsed.Address.Equals(value, StringComparison.OrdinalIgnoreCase), "Enter a valid email address.");
        return value.ToLowerInvariant();
    }
    public static void ValidatePassword(string password) => Guard.Require(password is { Length: >= 10 and <= 128 }, "Use a password between 10 and 128 characters.");
    public async Task<Account> Register(Registration input, CancellationToken ct)
    {
        var email = NormalizeEmail(input.Email); ValidatePassword(input.Password);
        Guard.Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim().Length <= 100, "Name is required (max 100 characters).");
        var account = new Account { Name = input.Name.Trim(), Email = email };
        account.PasswordHash = Hasher.HashPassword(account, input.Password);
        db.Accounts.Add(account);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { throw new ApiException(409, "This email already has an account. Please sign in."); }
        return account;
    }
    public async Task<Account> Login(SignIn input, CancellationToken ct)
    {
        Guard.Require(input.Email is { Length: > 0 and <= 254 } && input.Password is { Length: > 0 and <= 128 }, "Email and password are required.");
        var email = input.Email.Trim().ToLowerInvariant();
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Email == email, ct);
        var hasPassword = !string.IsNullOrWhiteSpace(account?.PasswordHash);
        var result = Hasher.VerifyHashedPassword(account ?? new Account(), hasPassword ? account!.PasswordHash : DummyHash, input.Password);
        if (account is null || !hasPassword || result == PasswordVerificationResult.Failed) throw new ApiException(401, "The email or password is incorrect.");
        if (result == PasswordVerificationResult.SuccessRehashNeeded) { account.PasswordHash = Hasher.HashPassword(account, input.Password); await db.SaveChangesAsync(ct); }
        return account;
    }
    public static string GoogleSubjectDigest(string subject) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject))).ToLowerInvariant();
    public async Task<Account> SignInWithGoogle(string subject, string email, string name, string? pictureUrl, bool emailVerified, CancellationToken ct)
    {
        Guard.Require(!string.IsNullOrWhiteSpace(subject) && subject.Length <= 255, "Google did not return a valid account identifier.");
        Guard.Require(emailVerified, "Verify your Google email before signing in to ShopSphere.");
        var normalizedEmail = NormalizeEmail(email);
        var digest = GoogleSubjectDigest(subject);
        var nameValue = string.IsNullOrWhiteSpace(name) ? normalizedEmail : name.Trim();
        Guard.Require(nameValue.Length <= 100, "Google profile name is too long.");
        var safePicture = Uri.TryCreate(pictureUrl, UriKind.Absolute, out var picture) && picture.Scheme == Uri.UriSchemeHttps && picture.AbsoluteUri.Length <= 2048
            ? picture.AbsoluteUri : null;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({digest}))", ct);
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.GoogleSubjectHash == digest, ct);
        if (account is null)
        {
            account = await db.Accounts.SingleOrDefaultAsync(a => a.Email == normalizedEmail, ct);
            if (account is null)
            {
                account = new Account { Email = normalizedEmail, Name = nameValue };
                db.Accounts.Add(account);
            }
            else if (account.GoogleSubjectHash is not null && account.GoogleSubjectHash != digest)
            {
                throw new ApiException(409, "This email is already linked to another Google account.");
            }
            account.GoogleSubjectHash = digest;
        }

        var emailOwner = await db.Accounts.SingleOrDefaultAsync(a => a.Email == normalizedEmail, ct);
        if (emailOwner is not null && emailOwner.Id != account.Id)
            throw new ApiException(409, "This Google email is already linked to another ShopSphere account.");
        account.Email = normalizedEmail;
        account.Name = nameValue;
        account.GooglePictureUrl = safePicture;
        account.GoogleLastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return account;
    }
    public async Task SeedAdministrator(IConfiguration configuration, CancellationToken ct)
    {
        var password = configuration["Admin:BootstrapPassword"];
        if (string.IsNullOrWhiteSpace(password)) return;
        var email = NormalizeEmail(configuration["Admin:BootstrapEmail"] ?? "admin@shopsphere.local");
        var existing = await db.Accounts.SingleOrDefaultAsync(a => a.Email == email, ct);
        if (existing is not null)
        {
            if (!existing.IsAdmin) throw new InvalidOperationException("The configured bootstrap email already belongs to a customer account. Choose a different admin email.");
            return;
        }
        ValidatePassword(password);
        var admin = new Account { Name = "Store Administrator", Email = email, IsAdmin = true };
        admin.PasswordHash = Hasher.HashPassword(admin, password);
        db.Accounts.Add(admin); await db.SaveChangesAsync(ct);
    }
    public static Task StartSession(HttpContext context, Account account, string provider = "local", string? customerId = null) => context.SignInAsync(StoreAuthentication.Scheme,
        new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Name, account.Name), new Claim(ClaimTypes.Email, account.Email),
            new Claim("customer_id", customerId ?? "local-" + account.Id.ToString("N")), new Claim("provider", provider), new Claim("local_admin", account.IsAdmin ? "true" : "false")
        }, StoreAuthentication.Scheme)));
}
public static class AccountEndpoints
{
    public static void MapLocalAccounts(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (Registration input, Accounts accounts, HttpContext context, CancellationToken ct) =>
        {
            var account = await accounts.Register(input, ct); await Accounts.StartSession(context, account); return Results.NoContent();
        });
        app.MapPost("/api/auth/login", async (SignIn input, Accounts accounts, HttpContext context, CancellationToken ct) =>
        {
            var account = await accounts.Login(input, ct); await Accounts.StartSession(context, account); return Results.NoContent();
        });
    }
}
