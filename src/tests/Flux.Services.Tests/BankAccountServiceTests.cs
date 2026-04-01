using Flux.Data;
using Flux.Data.Models;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Flux.Services.Tests;

public sealed class BankAccountServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly BankAccountService _service;

    public BankAccountServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _context.Database.EnsureCreated();

        _service = new BankAccountService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAllAccountsAsync_NonAdmin_ReturnsOnlyOwnedAccounts()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        _context.Accounts.AddRange(
            new BankAccount { OwnerUserId = userId, Owner = "user", AccountName = "mine", Balance = 100 },
            new BankAccount { OwnerUserId = otherId, Owner = "other", AccountName = "other", Balance = 200 });
        await _context.SaveChangesAsync();

        var results = (await _service.GetAllAccountsAsync(userId, isAdministrator: false)).ToList();

        Assert.Single(results);
        Assert.Equal(userId, results[0].OwnerUserId);
    }

    [Fact]
    public async Task GetAllAccountsAsync_Admin_ReturnsAllAccounts()
    {
        _context.Accounts.AddRange(
            new BankAccount { OwnerUserId = Guid.NewGuid(), Owner = "user1", AccountName = "a1", Balance = 100 },
            new BankAccount { OwnerUserId = Guid.NewGuid(), Owner = "user2", AccountName = "a2", Balance = 200 });
        await _context.SaveChangesAsync();

        var results = (await _service.GetAllAccountsAsync(Guid.NewGuid(), isAdministrator: true)).ToList();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task CreateAccountAsync_SetsOwnerAndDefaultNameAndTimestamps()
    {
        var userId = Guid.NewGuid();
        var input = new BankAccount
        {
            AccountName = "   ",
            Owner = string.Empty,
            Balance = 500,
            Type = AccountType.Checking
        };

        var created = await _service.CreateAccountAsync(input, userId, "owner-name");

        Assert.Equal(userId, created.OwnerUserId);
        Assert.Equal("owner-name", created.Owner);
        Assert.Equal("owner-name", created.AccountName);
        Assert.True(created.CreatedAt <= DateTime.UtcNow);
        Assert.True(created.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task UpdateAccountAsync_NonOwnerNonAdmin_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var existing = new BankAccount
        {
            OwnerUserId = ownerId,
            Owner = "owner",
            AccountName = "original",
            Balance = 100,
            Type = AccountType.Checking
        };

        _context.Accounts.Add(existing);
        await _context.SaveChangesAsync();

        var updated = new BankAccount
        {
            Id = existing.Id,
            OwnerUserId = ownerId,
            Owner = "owner",
            AccountName = "changed",
            Balance = 999,
            Type = AccountType.Savings
        };

        var success = await _service.UpdateAccountAsync(existing.Id, updated, otherId, isAdministrator: false);

        Assert.False(success);
    }

    [Fact]
    public async Task DeleteAccountAsync_NonOwnerNonAdmin_ReturnsFalse()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var existing = new BankAccount
        {
            OwnerUserId = ownerId,
            Owner = "owner",
            AccountName = "keep",
            Balance = 10,
            Type = AccountType.Checking
        };

        _context.Accounts.Add(existing);
        await _context.SaveChangesAsync();

        var success = await _service.DeleteAccountAsync(existing.Id, otherId, isAdministrator: false);

        Assert.False(success);
        Assert.Equal(1, await _context.Accounts.CountAsync());
    }

    [Fact]
    public async Task ImportAccountsAsync_Csv_UpsertsAndCreatesInSingleRequest()
    {
        var userId = Guid.NewGuid();
        var existing = new BankAccount
        {
            OwnerUserId = userId,
            Owner = "owner",
            AccountName = "Old Name",
            Balance = 100m,
            Type = AccountType.Checking
        };

        _context.Accounts.Add(existing);
        await _context.SaveChangesAsync();

        var csv = $"Id,AccountName,Balance,Type,CreditCardAprPercent,SavingsApyPercent\n" +
                  $"{existing.Id},Updated Name,250.5,Savings,,4.35\n" +
                  ",New Card,500,CreditCard,23.99,\n";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await _service.ImportAccountsAsync(stream, "accounts.csv", userId, "owner", isAdministrator: false, targetUserId: null);

        Assert.Equal(2, result.RowsProcessed);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.CreatedCount);

        var updated = await _context.Accounts.FindAsync(existing.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated!.AccountName);
        Assert.Equal(AccountType.Savings, updated.Type);
        Assert.Equal(4.35m, updated.SavingsApyPercent);
        Assert.Null(updated.CreditCardAprPercent);

        var created = await _context.Accounts
            .Where(account => account.Id != existing.Id)
            .SingleAsync();
        Assert.Equal("New Card", created.AccountName);
        Assert.Equal(AccountType.CreditCard, created.Type);
        Assert.Equal(23.99m, created.CreditCardAprPercent);
        Assert.Null(created.SavingsApyPercent);
    }

    [Fact]
    public async Task ImportAccountsAsync_WhenAnyRowInvalid_RollsBackAllChanges()
    {
        var userId = Guid.NewGuid();

        var csv = "Id,AccountName,Balance,Type,CreditCardAprPercent,SavingsApyPercent\n" +
                  ",Good Checking,100,Checking,,\n" +
                  ",Broken Type,200,NotAType,,\n";

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ImportAccountsAsync(
            stream,
            "accounts.csv",
            userId,
            "owner",
            isAdministrator: false,
            targetUserId: null));

        Assert.Equal(0, await _context.Accounts.CountAsync());
    }

    [Fact]
    public async Task ExportAccountsAsync_NonAdminWithTargetUser_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ExportAccountsAsync(
            Guid.NewGuid(),
            isAdministrator: false,
            targetUserId: Guid.NewGuid(),
            format: BankAccountFileFormat.Csv));
    }
}
