using Flux.Api.Startup;
using Flux.Data;
using Flux.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Flux.Api.IntegrationTests.StartupTests;

/// <summary>
/// Tests for <see cref="DatabaseSchemaInitializer"/> verifying that schema creation
/// succeeds on a fresh database, is safe to run multiple times (idempotent), and
/// leaves the EF-visible tables queryable afterwards.
/// </summary>
public sealed class DatabaseSchemaInitializerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly DatabaseSchemaInitializer _initializer;

    public DatabaseSchemaInitializerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _initializer = new DatabaseSchemaInitializer(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task InitializeSchemaAsync_OnFreshDatabase_CreatesBothEfTables()
    {
        await _initializer.InitializeSchemaAsync();

        // EF DbSet queries confirm the tables exist and are queryable
        var users = await _context.UserAccounts.ToListAsync();
        var accounts = await _context.Accounts.ToListAsync();

        Assert.Empty(users);
        Assert.Empty(accounts);
    }

    [Fact]
    public async Task InitializeSchemaAsync_IsIdempotent_DoesNotThrowWhenCalledTwice()
    {
        await _initializer.InitializeSchemaAsync();

        var exception = await Record.ExceptionAsync(() => _initializer.InitializeSchemaAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task InitializeSchemaAsync_UserAccountsTable_IncludesRoleColumn()
    {
        await _initializer.InitializeSchemaAsync();

        var user = new UserAccount
        {
            Username = "schema-test",
            Role = "Administrator",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            PasswordIterations = 100_000
        };

        _context.UserAccounts.Add(user);
        await _context.SaveChangesAsync();

        var saved = await _context.UserAccounts.SingleAsync();
        Assert.Equal("Administrator", saved.Role);
    }

    [Fact]
    public async Task InitializeSchemaAsync_AccountsTable_IncludesOwnershipNamingAndRateColumns()
    {
        await _initializer.InitializeSchemaAsync();

        var ownerUserId = Guid.NewGuid();
        var account = new BankAccount
        {
            OwnerUserId = ownerUserId,
            AccountName = "Schema Test Account",
            Owner = "schema-test",
            Balance = 1_000m,
            Type = AccountType.Savings,
            CreditCardAprPercent = null,
            SavingsApyPercent = 4.5m
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        var saved = await _context.Accounts.SingleAsync();
        Assert.Equal(ownerUserId, saved.OwnerUserId);
        Assert.Equal("Schema Test Account", saved.AccountName);
        Assert.Null(saved.CreditCardAprPercent);
        Assert.Equal(4.5m, saved.SavingsApyPercent);
    }
}
