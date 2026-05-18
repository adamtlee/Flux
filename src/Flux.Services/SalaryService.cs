using Flux.Services.Models;

namespace Flux.Services;

public sealed class SalaryService : ISalaryService
{
    private const int WorkingDaysPerYear = 260;   // 52 weeks × 5 days
    private const int WorkingHoursPerYear = 2080; // 260 days × 8 hours
    private const int BiweeklyPeriodsPerYear = 26;
    private const int WeeksPerYear = 52;
    private const int MonthsPerYear = 12;

    public SalaryCalculationResult Calculate(SalaryCalculationRequest request)
    {
        if (request.GrossAnnualSalary < 0)
        {
            throw new ArgumentException("Gross annual salary must be non-negative.", nameof(request));
        }

        var deductionBreakdown = ComputeDeductions(request.GrossAnnualSalary, request.Deductions);
        var totalDeductions = deductionBreakdown.Sum(d => d.AnnualAmount);
        var netAnnual = request.GrossAnnualSalary - totalDeductions;

        return new SalaryCalculationResult(
            GrossAnnual: request.GrossAnnualSalary,
            CurrencyCode: NormalizeCurrencyCode(request.CurrencyCode),
            DeductionBreakdown: deductionBreakdown,
            TotalDeductionsAnnual: totalDeductions,
            NetAnnual: netAnnual,
            NetMonthly: netAnnual / MonthsPerYear,
            NetBiweekly: netAnnual / BiweeklyPeriodsPerYear,
            NetWeekly: netAnnual / WeeksPerYear,
            NetDaily: netAnnual / WorkingDaysPerYear,
            NetHourly: netAnnual / WorkingHoursPerYear);
    }

    private static IReadOnlyList<SalaryDeductionResult> ComputeDeductions(
        decimal grossAnnual,
        IReadOnlyList<SalaryDeductionInput> deductions)
    {
        var results = new List<SalaryDeductionResult>();

        foreach (var deduction in deductions)
        {
            if (string.IsNullOrWhiteSpace(deduction.Name) || deduction.Value <= 0)
            {
                continue;
            }

            if (deduction.Type == SalaryDeductionType.Percent && deduction.Value > 100)
            {
                throw new ArgumentException(
                    $"Percent deduction '{deduction.Name}' cannot exceed 100%.",
                    nameof(deductions));
            }

            var annualAmount = deduction.Type == SalaryDeductionType.Percent
                ? grossAnnual * (deduction.Value / 100m)
                : deduction.Value;

            results.Add(new SalaryDeductionResult(deduction.Name.Trim(), annualAmount));
        }

        return results;
    }

    private static string NormalizeCurrencyCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "USD";
        }

        var trimmed = code.Trim().ToUpperInvariant();
        return trimmed.Length > 3 ? trimmed[..3] : trimmed;
    }
}
