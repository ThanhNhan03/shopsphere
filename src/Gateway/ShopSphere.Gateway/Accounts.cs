using System.Security.Claims;
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
}
public sealed class AccountsDb(DbContextOptions<AccountsDb> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Account>().HasIndex(a => a.Email).IsUnique();
        model.Entity<Account>().Property(a => a.Email).HasMaxLength(254);
        model.Entity<Account>().Property(a => a.Name).HasMaxLength(100);
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
        var result = Hasher.VerifyHashedPassword(account ?? new Account(), account?.PasswordHash ?? DummyHash, input.Password);
        if (account is null || result == PasswordVerificationResult.Failed) throw new ApiException(401, "The email or password is incorrect.");
        if (result == PasswordVerificationResult.SuccessRehashNeeded) { account.PasswordHash = Hasher.HashPassword(account, input.Password); await db.SaveChangesAsync(ct); }
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
    public static Task StartSession(HttpContext context, Account account) => context.SignInAsync(StoreAuthentication.Scheme,
        new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Name, account.Name), new Claim(ClaimTypes.Email, account.Email),
            new Claim("customer_id", "local-" + account.Id.ToString("N")), new Claim("provider", "local"), new Claim("local_admin", account.IsAdmin ? "true" : "false")
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
