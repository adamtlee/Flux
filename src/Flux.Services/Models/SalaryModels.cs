namespace Flux.Services.Models;

public enum SalaryDeductionType
{
    Percent = 0,
    FixedAnnual = 1
}

public sealed record SalaryDeductionInput(string Name, SalaryDeductionType Type, decimal Value);

public sealed record SalaryCalculationRequest(
    decimal GrossAnnualSalary,
    string CurrencyCode,
    IReadOnlyList<SalaryDeductionInput> Deductions);

public sealed record SalaryDeductionResult(string Name, decimal AnnualAmount);

public sealed record SalaryCalculationResult(
    decimal GrossAnnual,
    string CurrencyCode,
    IReadOnlyList<SalaryDeductionResult> DeductionBreakdown,
    decimal TotalDeductionsAnnual,
    decimal NetAnnual,
    decimal NetMonthly,
    decimal NetBiweekly,
    decimal NetWeekly,
    decimal NetDaily,
    decimal NetHourly);
