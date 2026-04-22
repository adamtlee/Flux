using Flux.Data.Models;

namespace Flux.Services.Models;

public sealed record SubscriptionUpsertModel(
    string ServiceName,
    string ProviderName,
    SubscriptionCategory Category,
    IReadOnlyList<string> Tags,
    SubscriptionBillingCycle BillingCycle,
    decimal Amount,
    string CurrencyCode,
    DateTime StartDateUtc,
    DateTime NextDueDateUtc,
    int ReminderDaysBeforeDue,
    bool AutoRenew,
    SubscriptionStatus Status,
    string? Notes
);

public sealed record SubscriptionQueryModel(
    SubscriptionCategory? Category,
    SubscriptionStatus? Status,
    int? DueWithinDays,
    string? Tag
);

public sealed record MonthlySpendPoint(
    int Year,
    int Month,
    decimal EstimatedSpend);

public sealed record CategorySpendBreakdown(
    SubscriptionCategory Category,
    decimal EstimatedMonthlySpend,
    int SubscriptionCount);

public sealed record SubscriptionSpendAnalyticsResponse(
    IReadOnlyList<MonthlySpendPoint> Trend,
    IReadOnlyList<CategorySpendBreakdown> CategoryBreakdown,
    decimal CurrentMonthlyEstimatedSpend
);
