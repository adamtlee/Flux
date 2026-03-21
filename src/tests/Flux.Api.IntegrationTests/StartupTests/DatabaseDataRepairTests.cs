using Flux.Api.Startup;
using Flux.Data;
using Flux.Data.Models;
using Flux.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Flux.Api.IntegrationTests.StartupTests;

/// <summary>
/// Unit tests for <see cref="DatabaseDataRepair"/> covering every repair branch:
/// no users, first-user role promotion, subsequent-user role assignment, orphan-account
/// reassignment, and missing account-name defaulting.
/// </summary>
public sealed class DatabaseDataRepairTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly DatabaseDataRepair _repair;

    public DatabaseDataRepairTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _context.Database.EnsureCreated();

        _repair = new DatabaseDataRepair(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RepairDataAsync_WithNoUsers_CompletesWithoutChangingAnything()
    {
        await _repair.RepairDataAsync();

        Assert.Equal(0, await _context.UserAccounts.CountAsync());
        Assert.Equal(0, await _context.Accounts.CountAsync());
    }

    [Fact]
    public async Task RepairDataAsync_FirstUserWithNoRole_IsPromotedToAdministrator()
    {
        var user = new UserAccount
        {
            Username = "first-user",
            Role = string.Empty,
            PasswordHash = "h",
            PasswordSalt = "s",
            PasswordIterations = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserAccounts.Add(user);
        await _context.SaveChangesAsync();

        await _repair.RepairDataAsync();

        var updated = await _context.UserAccounts.SingleAsync();
        Assert.Equal(ApplicationRoles.Administrator, updated.Role);
    }

    [Fact]
    public async Task RepairDataAsync_FirstUserWithWrongRole_IsPromotedToAdministrator()
    {
        var user = new UserAccount
        {
            Username = "demoted",
            Role = ApplicationRoles.FreeMember,
            PasswordHash = "h",
            PasswordSalt = "s",
            PasswordIterations = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserAccounts.Add(user);
        await _context.SaveChangesAsync();

        await _repair.RepairDataAsync();

        var updated = await _context.UserAccounts.SingleAsync();
        Assert.Equal(ApplicationRoles.Administrator, updated.Role);
    }

    [Fact]
    public async Task RepairDataAsync_SubsequentUserWithNoRole_GetsFreeMemberRole()
    {
        var first = new UserAccount
        {
            Username = "admin",
            Role = ApplicationRoles.Administrator,
            PasswordHash = "h",
            PasswordSalt = "s",
            PasswordIterations = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var second = new UserAccount
        {
            Username = "new-member",
            Role = string.Empty,
            PasswordHash = "h",
            PasswordSalt = "s",
            PasswordIterations = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserAccounts.AddRange(first, second);
        await _context.SaveChangesAsync();

        await _repair.RepairDataAsync();

        var member = await _context.UserAccounts.SingleAsync(u => u.Username == "new-member");
        Assert.Equal(ApplicationRoles.FreeMember, member.Role);
        // First user's role must remain untouched
        var admin = await _context.UserAccounts.SingleAsync(u => u.Username == "admin");
        Assert.Equal(ApplicationRoles.Administrator, admin.Role);
    }

    [Fact]
    public async Task RepairDataAsync_OrphanAccount_GetsAssignedToFirstUser()
    {
        var firstUser = new UserAccount
        {
            Username = "admin",
            Role = ApplicationRoles.Administrator,
            PasswordHash = "h",
            PasswordSalt = "s",
            PasswordIterations = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserAccounts.Add(firstUser);

        var orphan = new BankAccount
        {
            OwnerUserId = Guid.Empty,
            AccountName = "Orphan Account",
            Owner = string.Empty,
            Balance = 50m
        };
        _context.Accounts.Add(orphan);
        await _context.SaveChangesAsync();

        await _repair.RepairDataAsync();

        var repaired = await _context.Accounts.SingleAsync();
        Assert.Equal(firstUser.Id, repaired.OwnerUserId);
        Assert.Equal(firstUser.Username, repaired.Owner);
    }

    [Fact]
    public async Task RepairDataAsync_AccountWithEmptyName_ButHasOwner_DefaultsNameToOwner()
    {
        var firstUser = new UserAccount
        {
            Username = "admin",
            Role = ApplicationRoles.Administrator,
            PasswordHash = "h",
            PasswordSalt = "s",
            PasswordIterations = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserAccounts.Add(firstUser);

        var account = new BankAccount
        {
            OwnerUserId = firstUser.Id,
            Owner = "admin",
            AccountName = string.Empty,
            Balance = 0m
        };
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        await _repair.RepairDataAsync();

        var repaired = await _context.Accounts.SingleAsync();
        Assert.Equal("admin", repaired.AccountName);
    }

    [Fact]
    public async Task RepairDataAsync_AccountMissingBothNameAndOwner_DefaultsNameToFirstUsername()
    {
        var firstUser = new UserAccount
        {
            Username = "admin",
            Role = ApplicationRoles.Administrator,
            PasswordHash = "h",
            PasswordSalt = "s",
            PasswordIterations = 1,
            CreatedAt = DateTime.UtcNow
        };
        _context.UserAccounts.Add(firstUser);

        var account = new BankAccount
        {
            OwnerUserId = firstUser.Id,
            Owner = string.Empty,
            AccountName = string.Empty,
            Balance = 0m
        };
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        await _repair.RepairDataAsync();

        var repaired = await _context.Accounts.SingleAsync();
        Assert.Equal(firstUser.Username, repaired.AccountName);
    }
}
