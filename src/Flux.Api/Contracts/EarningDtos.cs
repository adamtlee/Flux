using System.ComponentModel.DataAnnotations;
using Flux.Data.Models;

namespace Flux.Api.Contracts;

public sealed class EarningCreateRequestDto
{
    [Required]
    [StringLength(150)]
    public string Label { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal AnnualGrossSalary { get; set; }

    [Required]
    public EarningDeductionMode DeductionMode { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal DeductionValue { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "USD";
}

public sealed class EarningUpdateRequestDto
{
    [Required]
    [StringLength(150)]
    public string Label { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal AnnualGrossSalary { get; set; }

    [Required]
    public EarningDeductionMode DeductionMode { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal DeductionValue { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "USD";
}

public sealed class EarningResponseDto
{
    public int Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string OwnerUsername { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public decimal AnnualGrossSalary { get; set; }

    public EarningDeductionMode DeductionMode { get; set; }

    public decimal DeductionValue { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class EarningsSummaryResponseDto
{
    public IReadOnlyList<EarningSummaryEntryDto> Entries { get; set; } = [];

    public EarningsPeriodBreakdownDto TotalGross { get; set; } = new();

    public EarningsPeriodBreakdownDto TotalNet { get; set; } = new();

    public decimal TotalAnnualDeductions { get; set; }
}

public sealed class EarningSummaryEntryDto
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public decimal AnnualGrossSalary { get; set; }

    public EarningDeductionMode DeductionMode { get; set; }

    public decimal DeductionValue { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public decimal AnnualDeduction { get; set; }

    public decimal AnnualNetSalary { get; set; }

    public EarningsPeriodBreakdownDto GrossBreakdown { get; set; } = new();

    public EarningsPeriodBreakdownDto NetBreakdown { get; set; } = new();
}

public sealed class EarningsPeriodBreakdownDto
{
    public decimal Annual { get; set; }

    public decimal Monthly { get; set; }

    public decimal BiWeekly { get; set; }

    public decimal Weekly { get; set; }

    public decimal Daily { get; set; }

    public decimal Hourly { get; set; }
}