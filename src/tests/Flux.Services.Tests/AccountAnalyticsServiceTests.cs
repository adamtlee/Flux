using Flux.Data;
using Flux.Data.Models;
using Flux.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Flux.Services.Tests;

public sealed class AccountAnalyticsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly AccountAnalyticsService _service;

    public AccountAnalyticsServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _context.Database.EnsureCreated();

        var analyticsOptions = Options.Create(new RateAnalyticsOptions
        {
            CreditCard = new RateAnalyticsOptions.CreditCardDefaults
            {
                AprPercent = 20m,
                MinimumPaymentPercent = 2m,
                MinimumPaymentFlatAmount = 25m
            },
            Savings = new RateAnalyticsOptions.SavingsDefaults
            {
                ApyPercent = 5m
            }
        });

        _service = new AccountAnalyticsService(_context, analyticsOptions);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPortfolioAnalyticsAsync_UsesRateOverridesAndDefaults()
    {
        var userId = Guid.NewGuid();

        _context.Accounts.AddRange(
            new BankAccount
            {
                OwnerUserId = userId,
                Owner = "member",
                AccountName = "Card Default",
                Balance = 1_000m,
                Type = AccountType.CreditCard
            },
            new BankAccount
            {
                OwnerUserId = userId,
                Owner = "member",
                AccountName = "Card Override",
                Balance = 500m,
                Type = AccountType.CreditCard,
                CreditCardAprPercent = 12m
            },
            new BankAccount
            {
                OwnerUserId = userId,
                Owner = "member",
                AccountName = "Savings Override",
                Balance = 2_000m,
                Type = AccountType.Savings,
                SavingsApyPercent = 6m
            });

        await _context.SaveChangesAsync();

        var analytics = await _service.GetPortfolioAnalyticsAsync(userId, isAdministrator: false);

        Assert.Equal(2, analytics.CreditCardSummary.CardCount);
        Assert.Equal(1500m, analytics.CreditCardSummary.TotalBalance);
        Assert.Equal(16m, analytics.CreditCardSummary.AverageAprPercent);

        Assert.Single(analytics.SavingsAccounts);
        Assert.Equal(6m, analytics.SavingsAccounts[0].ApyPercent);
        Assert.Equal(3, analytics.SavingsAccounts[0].CompoundingScenarios.Count);
    }

    [Fact]
    public async Task GetAccountAnalyticsByIdAsync_NonOwner_ReturnsNull()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var account = new BankAccount
        {
            OwnerUserId = ownerId,
            Owner = "owner",
            AccountName = "Private Card",
            Balance = 500m,
            Type = AccountType.CreditCard
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        var analytics = await _service.GetAccountAnalyticsByIdAsync(account.Id, otherUserId, isAdministrator: false);

        Assert.Null(analytics);
    }

    [Fact]
    public async Task GetAccountAnalyticsByIdAsync_CreditCard_ReturnsPayoffProjection()
    {
        var userId = Guid.NewGuid();

        var account = new BankAccount
        {
            OwnerUserId = userId,
            Owner = "member",
            AccountName = "Projection Card",
            Balance = 800m,
            Type = AccountType.CreditCard,
            CreditCardAprPercent = 24m
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        var analytics = await _service.GetAccountAnalyticsByIdAsync(account.Id, userId, isAdministrator: false);

        Assert.NotNull(analytics);
        Assert.NotNull(analytics!.CreditCard);
        Assert.Equal(24m, analytics.CreditCard!.AprPercent);
        Assert.True(analytics.CreditCard.MinimumPaymentAmount > 0m);
        Assert.True(analytics.CreditCard.EstimatedPayoffMonths is null || analytics.CreditCard.EstimatedPayoffMonths > 0);
    }
}
