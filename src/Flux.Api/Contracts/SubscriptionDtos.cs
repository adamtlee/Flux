using System.ComponentModel.DataAnnotations;
using Flux.Data.Models;

namespace Flux.Api.Contracts;

public sealed class SubscriptionCreateRequestDto
{
    [Required]
    [StringLength(150)]
    public string ServiceName { get; set; } = string.Empty;

    [StringLength(150)]
    public string ProviderName { get; set; } = string.Empty;

    [Required]
    public SubscriptionCategory Category { get; set; }

    [MaxLength(20)]
    public List<string> Tags { get; set; } = [];

    [Required]
    public SubscriptionBillingCycle BillingCycle { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Required]
    public DateTime StartDateUtc { get; set; }

    [Required]
    public DateTime NextDueDateUtc { get; set; }

    [Range(0, 60)]
    public int ReminderDaysBeforeDue { get; set; } = 3;

    public bool AutoRenew { get; set; } = true;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class SubscriptionUpdateRequestDto
{
    [Required]
    [StringLength(150)]
    public string ServiceName { get; set; } = string.Empty;

    [StringLength(150)]
    public string ProviderName { get; set; } = string.Empty;

    [Required]
    public SubscriptionCategory Category { get; set; }

    [MaxLength(20)]
    public List<string> Tags { get; set; } = [];

    [Required]
    public SubscriptionBillingCycle BillingCycle { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Required]
    public DateTime StartDateUtc { get; set; }

    [Required]
    public DateTime NextDueDateUtc { get; set; }

    [Range(0, 60)]
    public int ReminderDaysBeforeDue { get; set; } = 3;

    public bool AutoRenew { get; set; } = true;

    public SubscriptionStatus Status { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class SubscriptionResponseDto
{
    public int Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string OwnerUsername { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public SubscriptionCategory Category { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = [];

    public SubscriptionBillingCycle BillingCycle { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public DateTime StartDateUtc { get; set; }

    public DateTime NextDueDateUtc { get; set; }

    public int ReminderDaysBeforeDue { get; set; }

    public bool AutoRenew { get; set; }

    public SubscriptionStatus Status { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class SubscriptionAnalyticsResponseDto
{
    public IReadOnlyList<SubscriptionTrendPointDto> Trend { get; set; } = [];

    public IReadOnlyList<SubscriptionCategorySpendDto> CategoryBreakdown { get; set; } = [];

    public decimal CurrentMonthlyEstimatedSpend { get; set; }
}

public sealed class SubscriptionTrendPointDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public decimal EstimatedSpend { get; set; }
}

public sealed class SubscriptionCategorySpendDto
{
    public SubscriptionCategory Category { get; set; }

    public decimal EstimatedMonthlySpend { get; set; }

    public int SubscriptionCount { get; set; }
}
