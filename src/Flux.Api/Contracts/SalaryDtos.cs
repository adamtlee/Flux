using System.ComponentModel.DataAnnotations;
using Flux.Services.Models;

namespace Flux.Api.Contracts;

public sealed class SalaryCalculateRequestDto
{
    [Required]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal GrossAnnualSalary { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    public List<SalaryDeductionInputDto> Deductions { get; set; } = [];
}

public sealed class SalaryDeductionInputDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public SalaryDeductionType Type { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Value { get; set; }
}

public sealed class SalaryCalculationResponseDto
{
    public decimal GrossAnnual { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public List<SalaryDeductionResultDto> DeductionBreakdown { get; set; } = [];
    public decimal TotalDeductionsAnnual { get; set; }
    public decimal NetAnnual { get; set; }
    public decimal NetMonthly { get; set; }
    public decimal NetBiweekly { get; set; }
    public decimal NetWeekly { get; set; }
    public decimal NetDaily { get; set; }
    public decimal NetHourly { get; set; }
}

public sealed class SalaryDeductionResultDto
{
    public string Name { get; set; } = string.Empty;
    public decimal AnnualAmount { get; set; }
}
