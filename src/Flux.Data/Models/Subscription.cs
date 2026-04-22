namespace Flux.Data.Models;

public sealed class Subscription
{
    public int Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string OwnerUsername { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public SubscriptionCategory Category { get; set; }

    public string TagsCsv { get; set; } = string.Empty;

    public SubscriptionBillingCycle BillingCycle { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public DateTime StartDateUtc { get; set; }

    public DateTime NextDueDateUtc { get; set; }

    public int ReminderDaysBeforeDue { get; set; }

    public bool AutoRenew { get; set; } = true;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public string? Notes { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SubscriptionCategory
{
    Entertainment,
    Insurance,
    Utilities,
    Mobile,
    Internet,
    Productivity,
    Health,
    Education,
    Transportation,
    Other
}

public enum SubscriptionBillingCycle
{
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

public enum SubscriptionStatus
{
    Active,
    Paused,
    Cancelled
}
