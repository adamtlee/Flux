using Flux.Data;
using Flux.Data.Models;
using Flux.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace Flux.Services;

public sealed class SubscriptionService(BankDbContext context) : ISubscriptionService
{
    public async Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(Guid userId, bool isAdministrator, SubscriptionQueryModel query)
    {
        var subscriptionsQuery = BuildScopedQuery(userId, isAdministrator);

        if (query.Category.HasValue)
        {
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.Category == query.Category.Value);
        }

        if (query.Status.HasValue)
        {
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.Status == query.Status.Value);
        }

        if (query.DueWithinDays.HasValue)
        {
            var withinDays = Math.Clamp(query.DueWithinDays.Value, 0, 365);
            var dueDateLimit = DateTime.UtcNow.Date.AddDays(withinDays);
            subscriptionsQuery = subscriptionsQuery.Where(subscription => subscription.NextDueDateUtc.Date <= dueDateLimit);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var normalizedTag = NormalizeTag(query.Tag);
            subscriptionsQuery = subscriptionsQuery.Where(subscription => EF.Functions.Like(subscription.TagsCsv, $"%{normalizedTag}%"));
        }

        return await subscriptionsQuery
            .AsNoTracking()
            .OrderBy(subscription => subscription.NextDueDateUtc)
            .ThenBy(subscription => subscription.ServiceName)
            .ToListAsync();
    }

    public async Task<Subscription?> GetSubscriptionByIdAsync(int id, Guid userId, bool isAdministrator)
    {
        var subscription = await context.Subscriptions.FirstOrDefaultAsync(item => item.Id == id);
        if (subscription is null)
        {
            return null;
        }

        if (!isAdministrator && subscription.OwnerUserId != userId)
        {
            return null;
        }

        return subscription;
    }

    public async Task<Subscription> CreateSubscriptionAsync(Guid userId, string username, SubscriptionUpsertModel model)
    {
        ValidateModel(model);

        var utcNow = DateTime.UtcNow;
        var subscription = new Subscription
        {
            OwnerUserId = userId,
            OwnerUsername = username,
            ServiceName = model.ServiceName.Trim(),
            ProviderName = NormalizeProviderName(model.ProviderName, model.ServiceName),
            Category = model.Category,
            TagsCsv = BuildTagsCsv(model.Tags),
            BillingCycle = model.BillingCycle,
            Amount = model.Amount,
            CurrencyCode = NormalizeCurrencyCode(model.CurrencyCode),
            StartDateUtc = NormalizeUtc(model.StartDateUtc),
            NextDueDateUtc = NormalizeUtc(model.NextDueDateUtc),
            ReminderDaysBeforeDue = model.ReminderDaysBeforeDue,
            AutoRenew = model.AutoRenew,
            Status = model.Status == SubscriptionStatus.Cancelled ? SubscriptionStatus.Active : model.Status,
            Notes = NormalizeNotes(model.Notes),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();
        return subscription;
    }

    public async Task<Subscription?> UpdateSubscriptionAsync(int id, Guid userId, bool isAdministrator, SubscriptionUpsertModel model)
    {
        ValidateModel(model);

        var subscription = await context.Subscriptions.FirstOrDefaultAsync(item => item.Id == id);
        if (subscription is null)
        {
            return null;
        }

        if (!isAdministrator && subscription.OwnerUserId != userId)
        {
            return null;
        }

        subscription.ServiceName = model.ServiceName.Trim();
        subscription.ProviderName = NormalizeProviderName(model.ProviderName, model.ServiceName);
        subscription.Category = model.Category;
        subscription.TagsCsv = BuildTagsCsv(model.Tags);
        subscription.BillingCycle = model.BillingCycle;
        subscription.Amount = model.Amount;
        subscription.CurrencyCode = NormalizeCurrencyCode(model.CurrencyCode);
        subscription.StartDateUtc = NormalizeUtc(model.StartDateUtc);
        subscription.NextDueDateUtc = NormalizeUtc(model.NextDueDateUtc);
        subscription.ReminderDaysBeforeDue = model.ReminderDaysBeforeDue;
        subscription.AutoRenew = model.AutoRenew;
        subscription.Status = model.Status;
        subscription.Notes = NormalizeNotes(model.Notes);
        subscription.CancelledAtUtc = model.Status == SubscriptionStatus.Cancelled
            ? subscription.CancelledAtUtc ?? DateTime.UtcNow
            : null;
        subscription.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return subscription;
    }

    public async Task<bool> CancelSubscriptionAsync(int id, Guid userId, bool isAdministrator)
    {
        var subscription = await context.Subscriptions.FirstOrDefaultAsync(item => item.Id == id);
        if (subscription is null)
        {
            return false;
        }

        if (!isAdministrator && subscription.OwnerUserId != userId)
        {
            return false;
        }

        if (subscription.Status == SubscriptionStatus.Cancelled)
        {
            return true;
        }

        var utcNow = DateTime.UtcNow;
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAtUtc = utcNow;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = utcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<Subscription>> GetUpcomingRemindersAsync(Guid userId, bool isAdministrator, int withinDays)
    {
        var clampedWithinDays = Math.Clamp(withinDays, 0, 365);
        var startDate = DateTime.UtcNow.Date;
        var dueDateLimit = startDate.AddDays(clampedWithinDays);

        var subscriptions = await BuildScopedQuery(userId, isAdministrator)
            .Where(subscription => subscription.Status != SubscriptionStatus.Cancelled)
            .Where(subscription => subscription.NextDueDateUtc.Date >= startDate && subscription.NextDueDateUtc.Date <= dueDateLimit)
            .AsNoTracking()
            .OrderBy(subscription => subscription.NextDueDateUtc)
            .ThenBy(subscription => subscription.ServiceName)
            .ToListAsync();

        return subscriptions
            .Where(subscription => GetReminderDate(subscription) <= DateTime.UtcNow.Date)
            .ToList();
    }

    public async Task<SubscriptionSpendAnalyticsResponse> GetMonthlySpendAnalyticsAsync(Guid userId, bool isAdministrator, int months)
    {
        var clampedMonths = Math.Clamp(months, 1, 24);
        var utcNow = DateTime.UtcNow;

        var subscriptions = await BuildScopedQuery(userId, isAdministrator)
            .AsNoTracking()
            .ToListAsync();

        var trend = BuildMonthlyTrend(subscriptions, utcNow, clampedMonths);
        var categoryBreakdown = BuildCategoryBreakdown(subscriptions, utcNow);
        var currentMonthlyEstimatedSpend = categoryBreakdown.Sum(item => item.EstimatedMonthlySpend);

        return new SubscriptionSpendAnalyticsResponse(trend, categoryBreakdown, currentMonthlyEstimatedSpend);
    }

    private IQueryable<Subscription> BuildScopedQuery(Guid userId, bool isAdministrator)
    {
        var query = context.Subscriptions.AsQueryable();
        if (!isAdministrator)
        {
            query = query.Where(subscription => subscription.OwnerUserId == userId);
        }

        return query;
    }

    private static List<MonthlySpendPoint> BuildMonthlyTrend(IReadOnlyList<Subscription> subscriptions, DateTime utcNow, int months)
    {
        var trend = new List<MonthlySpendPoint>(months);

        for (var monthOffset = months - 1; monthOffset >= 0; monthOffset--)
        {
            var start = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-monthOffset);
            var end = start.AddMonths(1).AddTicks(-1);

            var monthTotal = subscriptions
                .Where(subscription => IsActiveInWindow(subscription, start, end))
                .Sum(subscription => ToEstimatedMonthlyCost(subscription.Amount, subscription.BillingCycle));

            trend.Add(new MonthlySpendPoint(start.Year, start.Month, monthTotal));
        }

        return trend;
    }

    private static List<CategorySpendBreakdown> BuildCategoryBreakdown(IReadOnlyList<Subscription> subscriptions, DateTime utcNow)
    {
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        return subscriptions
            .Where(subscription => IsActiveInWindow(subscription, monthStart, monthEnd))
            .GroupBy(subscription => subscription.Category)
            .Select(group => new CategorySpendBreakdown(
                group.Key,
                group.Sum(item => ToEstimatedMonthlyCost(item.Amount, item.BillingCycle)),
                group.Count()))
            .OrderByDescending(item => item.EstimatedMonthlySpend)
            .ThenBy(item => item.Category)
            .ToList();
    }

    private static bool IsActiveInWindow(Subscription subscription, DateTime start, DateTime end)
    {
        if (subscription.StartDateUtc > end)
        {
            return false;
        }

        if (subscription.Status == SubscriptionStatus.Cancelled && subscription.CancelledAtUtc is null)
        {
            return false;
        }

        if (subscription.CancelledAtUtc.HasValue && subscription.CancelledAtUtc.Value < start)
        {
            return false;
        }

        return true;
    }

    private static decimal ToEstimatedMonthlyCost(decimal amount, SubscriptionBillingCycle billingCycle)
    {
        return billingCycle switch
        {
            SubscriptionBillingCycle.Weekly => Math.Round((amount * 52m) / 12m, 2, MidpointRounding.AwayFromZero),
            SubscriptionBillingCycle.Monthly => amount,
            SubscriptionBillingCycle.Quarterly => Math.Round(amount / 3m, 2, MidpointRounding.AwayFromZero),
            SubscriptionBillingCycle.Yearly => Math.Round(amount / 12m, 2, MidpointRounding.AwayFromZero),
            _ => amount
        };
    }

    private static DateTime GetReminderDate(Subscription subscription)
    {
        return subscription.NextDueDateUtc.Date.AddDays(-subscription.ReminderDaysBeforeDue);
    }

    private static void ValidateModel(SubscriptionUpsertModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ServiceName))
        {
            throw new ArgumentException("ServiceName is required.");
        }

        if (model.Amount <= 0m)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        if (model.StartDateUtc == default)
        {
            throw new ArgumentException("StartDateUtc is required.");
        }

        if (model.NextDueDateUtc == default)
        {
            throw new ArgumentException("NextDueDateUtc is required.");
        }

        if (NormalizeUtc(model.NextDueDateUtc) < NormalizeUtc(model.StartDateUtc))
        {
            throw new ArgumentException("NextDueDateUtc must be on or after StartDateUtc.");
        }

        if (model.ReminderDaysBeforeDue is < 0 or > 60)
        {
            throw new ArgumentException("ReminderDaysBeforeDue must be between 0 and 60.");
        }

        if (!string.IsNullOrWhiteSpace(model.CurrencyCode) && model.CurrencyCode.Trim().Length != 3)
        {
            throw new ArgumentException("CurrencyCode must be a 3-letter ISO currency code.");
        }

        foreach (var tag in model.Tags)
        {
            var normalizedTag = NormalizeTag(tag);
            if (normalizedTag.Length is < 1 or > 40)
            {
                throw new ArgumentException("Each tag must be between 1 and 40 characters.");
            }
        }
    }

    private static string BuildTagsCsv(IReadOnlyList<string> tags)
    {
        var normalizedTags = tags
            .Select(NormalizeTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        return string.Join(',', normalizedTags);
    }

    private static string NormalizeTag(string tag)
    {
        return string.IsNullOrWhiteSpace(tag)
            ? string.Empty
            : tag.Trim().ToLowerInvariant();
    }

    private static string NormalizeCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return "USD";
        }

        return currencyCode.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        return notes.Trim();
    }

    private static string NormalizeProviderName(string providerName, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return serviceName.Trim();
        }

        return providerName.Trim();
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
