using Flux.Data;
using Flux.Data.Models;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Flux.Services.Tests;

public sealed class SubscriptionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _context.Database.EnsureCreated();

        _service = new SubscriptionService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateSubscriptionAsync_ValidRequest_PersistsAndNormalizesFields()
    {
        var userId = Guid.NewGuid();
        var model = new SubscriptionUpsertModel(
            ServiceName: " Spotify ",
            ProviderName: string.Empty,
            Category: SubscriptionCategory.Entertainment,
            Tags: [" Music ", "music", "streaming"],
            BillingCycle: SubscriptionBillingCycle.Monthly,
            Amount: 11.99m,
            CurrencyCode: "usd",
            StartDateUtc: DateTime.UtcNow.Date,
            NextDueDateUtc: DateTime.UtcNow.Date.AddDays(5),
            ReminderDaysBeforeDue: 3,
            AutoRenew: true,
            Status: SubscriptionStatus.Active,
            Notes: "  Family plan  ");

        var created = await _service.CreateSubscriptionAsync(userId, "member", model);

        Assert.True(created.Id > 0);
        Assert.Equal("Spotify", created.ServiceName);
        Assert.Equal("Spotify", created.ProviderName);
        Assert.Equal("USD", created.CurrencyCode);
        Assert.Equal("music,streaming", created.TagsCsv);
        Assert.Equal(userId, created.OwnerUserId);
    }

    [Fact]
    public async Task GetSubscriptionsAsync_NonAdmin_ReturnsOwnedSubscriptionsOnly()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        _context.Subscriptions.AddRange(
            new Subscription
            {
                OwnerUserId = ownerId,
                OwnerUsername = "owner",
                ServiceName = "Spotify",
                ProviderName = "Spotify",
                Category = SubscriptionCategory.Entertainment,
                TagsCsv = "music",
                BillingCycle = SubscriptionBillingCycle.Monthly,
                Amount = 9.99m,
                CurrencyCode = "USD",
                StartDateUtc = DateTime.UtcNow.Date.AddMonths(-2),
                NextDueDateUtc = DateTime.UtcNow.Date.AddDays(2),
                ReminderDaysBeforeDue = 2,
                Status = SubscriptionStatus.Active
            },
            new Subscription
            {
                OwnerUserId = otherId,
                OwnerUsername = "other",
                ServiceName = "Insurance",
                ProviderName = "Care Insurance",
                Category = SubscriptionCategory.Insurance,
                TagsCsv = "insurance",
                BillingCycle = SubscriptionBillingCycle.Monthly,
                Amount = 100m,
                CurrencyCode = "USD",
                StartDateUtc = DateTime.UtcNow.Date.AddMonths(-6),
                NextDueDateUtc = DateTime.UtcNow.Date.AddDays(15),
                ReminderDaysBeforeDue = 3,
                Status = SubscriptionStatus.Active
            });

        await _context.SaveChangesAsync();

        var results = await _service.GetSubscriptionsAsync(
            ownerId,
            isAdministrator: false,
            new SubscriptionQueryModel(null, null, null, null));

        Assert.Single(results);
        Assert.Equal(ownerId, results[0].OwnerUserId);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_ExistingSubscription_SetsCancelledState()
    {
        var userId = Guid.NewGuid();
        var subscription = new Subscription
        {
            OwnerUserId = userId,
            OwnerUsername = "member",
            ServiceName = "Cell Phone",
            ProviderName = "Carrier",
            Category = SubscriptionCategory.Mobile,
            TagsCsv = "phone",
            BillingCycle = SubscriptionBillingCycle.Monthly,
            Amount = 70m,
            CurrencyCode = "USD",
            StartDateUtc = DateTime.UtcNow.Date.AddMonths(-4),
            NextDueDateUtc = DateTime.UtcNow.Date.AddDays(7),
            ReminderDaysBeforeDue = 2,
            Status = SubscriptionStatus.Active
        };

        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        var cancelled = await _service.CancelSubscriptionAsync(subscription.Id, userId, isAdministrator: false);

        Assert.True(cancelled);

        var saved = await _context.Subscriptions.SingleAsync();
        Assert.Equal(SubscriptionStatus.Cancelled, saved.Status);
        Assert.False(saved.AutoRenew);
        Assert.NotNull(saved.CancelledAtUtc);
    }

    [Fact]
    public async Task GetUpcomingRemindersAsync_ReturnsOnlyReminderReadySubscriptions()
    {
        var userId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        _context.Subscriptions.AddRange(
            new Subscription
            {
                OwnerUserId = userId,
                OwnerUsername = "member",
                ServiceName = "Spotify",
                ProviderName = "Spotify",
                Category = SubscriptionCategory.Entertainment,
                TagsCsv = "music",
                BillingCycle = SubscriptionBillingCycle.Monthly,
                Amount = 12m,
                CurrencyCode = "USD",
                StartDateUtc = today.AddMonths(-6),
                NextDueDateUtc = today.AddDays(2),
                ReminderDaysBeforeDue = 3,
                Status = SubscriptionStatus.Active
            },
            new Subscription
            {
                OwnerUserId = userId,
                OwnerUsername = "member",
                ServiceName = "Insurance",
                ProviderName = "Care",
                Category = SubscriptionCategory.Insurance,
                TagsCsv = "insurance",
                BillingCycle = SubscriptionBillingCycle.Monthly,
                Amount = 75m,
                CurrencyCode = "USD",
                StartDateUtc = today.AddMonths(-2),
                NextDueDateUtc = today.AddDays(10),
                ReminderDaysBeforeDue = 1,
                Status = SubscriptionStatus.Active
            });

        await _context.SaveChangesAsync();

        var reminders = await _service.GetUpcomingRemindersAsync(userId, isAdministrator: false, withinDays: 14);

        Assert.Single(reminders);
        Assert.Equal("Spotify", reminders[0].ServiceName);
    }

    [Fact]
    public async Task GetMonthlySpendAnalyticsAsync_ReturnsTrendAndCategoryBreakdown()
    {
        var userId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        _context.Subscriptions.AddRange(
            new Subscription
            {
                OwnerUserId = userId,
                OwnerUsername = "member",
                ServiceName = "Spotify",
                ProviderName = "Spotify",
                Category = SubscriptionCategory.Entertainment,
                TagsCsv = "music",
                BillingCycle = SubscriptionBillingCycle.Monthly,
                Amount = 10m,
                CurrencyCode = "USD",
                StartDateUtc = today.AddMonths(-5),
                NextDueDateUtc = today.AddDays(5),
                ReminderDaysBeforeDue = 2,
                Status = SubscriptionStatus.Active
            },
            new Subscription
            {
                OwnerUserId = userId,
                OwnerUsername = "member",
                ServiceName = "Gym",
                ProviderName = "Fit Center",
                Category = SubscriptionCategory.Health,
                TagsCsv = "fitness",
                BillingCycle = SubscriptionBillingCycle.Yearly,
                Amount = 120m,
                CurrencyCode = "USD",
                StartDateUtc = today.AddMonths(-10),
                NextDueDateUtc = today.AddMonths(1),
                ReminderDaysBeforeDue = 7,
                Status = SubscriptionStatus.Active
            });

        await _context.SaveChangesAsync();

        var analytics = await _service.GetMonthlySpendAnalyticsAsync(userId, isAdministrator: false, months: 6);

        Assert.Equal(6, analytics.Trend.Count);
        Assert.True(analytics.CurrentMonthlyEstimatedSpend > 0m);
        Assert.Contains(analytics.CategoryBreakdown, item => item.Category == SubscriptionCategory.Entertainment);
    }
}
