using Flux.Data;
using Flux.Data.Models;
using Flux.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace Flux.Services;

public sealed class EarningsService(BankDbContext context) : IEarningsService
{
    private const decimal MonthsPerYear = 12m;
    private const decimal BiWeeklyPeriodsPerYear = 26m;
    private const decimal WeeksPerYear = 52m;
    private const decimal WorkDaysPerYear = 260m;
    private const decimal WorkHoursPerYear = 2080m;

    public async Task<IReadOnlyList<Earning>> GetEarningsAsync(Guid userId, bool isAdministrator)
    {
        return await BuildScopedQuery(userId, isAdministrator)
            .AsNoTracking()
            .OrderBy(earning => earning.Label)
            .ThenByDescending(earning => earning.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Earning?> GetEarningByIdAsync(int id, Guid userId, bool isAdministrator)
    {
        var earning = await context.Earnings.FirstOrDefaultAsync(item => item.Id == id);
        if (earning is null)
        {
            return null;
        }

        if (!isAdministrator && earning.OwnerUserId != userId)
        {
            return null;
        }

        return earning;
    }

    public async Task<Earning> CreateEarningAsync(Guid userId, string username, EarningUpsertModel model)
    {
        ValidateModel(model);

        var utcNow = DateTime.UtcNow;
        var earning = new Earning
        {
            OwnerUserId = userId,
            OwnerUsername = username,
            Label = NormalizeLabel(model.Label),
            AnnualGrossSalary = NormalizeMoney(model.AnnualGrossSalary),
            DeductionMode = model.DeductionMode,
            DeductionValue = NormalizeStoredDeductionValue(model.DeductionMode, model.DeductionValue),
            CurrencyCode = NormalizeCurrencyCode(model.CurrencyCode),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        context.Earnings.Add(earning);
        await context.SaveChangesAsync();
        return earning;
    }

    public async Task<Earning?> UpdateEarningAsync(int id, Guid userId, bool isAdministrator, EarningUpsertModel model)
    {
        ValidateModel(model);

        var earning = await context.Earnings.FirstOrDefaultAsync(item => item.Id == id);
        if (earning is null)
        {
            return null;
        }

        if (!isAdministrator && earning.OwnerUserId != userId)
        {
            return null;
        }

        earning.Label = NormalizeLabel(model.Label);
        earning.AnnualGrossSalary = NormalizeMoney(model.AnnualGrossSalary);
        earning.DeductionMode = model.DeductionMode;
        earning.DeductionValue = NormalizeStoredDeductionValue(model.DeductionMode, model.DeductionValue);
        earning.CurrencyCode = NormalizeCurrencyCode(model.CurrencyCode);
        earning.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return earning;
    }

    public async Task<bool> DeleteEarningAsync(int id, Guid userId, bool isAdministrator)
    {
        var earning = await context.Earnings.FirstOrDefaultAsync(item => item.Id == id);
        if (earning is null)
        {
            return false;
        }

        if (!isAdministrator && earning.OwnerUserId != userId)
        {
            return false;
        }

        context.Earnings.Remove(earning);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<EarningsSummaryResponse> GetSummaryAsync(Guid userId, bool isAdministrator)
    {
        var earnings = await BuildScopedQuery(userId, isAdministrator)
            .AsNoTracking()
            .OrderBy(earning => earning.Label)
            .ThenByDescending(earning => earning.UpdatedAt)
            .ToListAsync();

        var entries = earnings.Select(BuildSummaryEntry).ToList();
        var totalAnnualGross = entries.Sum(item => item.GrossBreakdown.Annual);
        var totalAnnualNet = entries.Sum(item => item.NetBreakdown.Annual);
        var totalAnnualDeductions = entries.Sum(item => item.AnnualDeduction);

        return new EarningsSummaryResponse(
            entries,
            BuildBreakdown(totalAnnualGross),
            BuildBreakdown(totalAnnualNet),
            NormalizeMoney(totalAnnualDeductions));
    }

    private IQueryable<Earning> BuildScopedQuery(Guid userId, bool isAdministrator)
    {
        var query = context.Earnings.AsQueryable();
        if (!isAdministrator)
        {
            query = query.Where(earning => earning.OwnerUserId == userId);
        }

        return query;
    }

    private static EarningSummaryEntry BuildSummaryEntry(Earning earning)
    {
        var annualGrossSalary = NormalizeMoney(earning.AnnualGrossSalary);
        var annualDeduction = CalculateAnnualDeduction(annualGrossSalary, earning.DeductionMode, earning.DeductionValue);
        var annualNetSalary = NormalizeMoney(Math.Max(0m, annualGrossSalary - annualDeduction));

        return new EarningSummaryEntry(
            earning.Id,
            earning.Label,
            annualGrossSalary,
            earning.DeductionMode,
            NormalizeMoney(earning.DeductionValue),
            NormalizeCurrencyCode(earning.CurrencyCode),
            annualDeduction,
            annualNetSalary,
            BuildBreakdown(annualGrossSalary),
            BuildBreakdown(annualNetSalary));
    }

    private static EarningsPeriodBreakdown BuildBreakdown(decimal annualAmount)
    {
        var normalizedAnnualAmount = NormalizeMoney(annualAmount);

        return new EarningsPeriodBreakdown(
            normalizedAnnualAmount,
            NormalizeMoney(normalizedAnnualAmount / MonthsPerYear),
            NormalizeMoney(normalizedAnnualAmount / BiWeeklyPeriodsPerYear),
            NormalizeMoney(normalizedAnnualAmount / WeeksPerYear),
            NormalizeMoney(normalizedAnnualAmount / WorkDaysPerYear),
            NormalizeMoney(normalizedAnnualAmount / WorkHoursPerYear));
    }

    private static decimal CalculateAnnualDeduction(decimal annualGrossSalary, EarningDeductionMode deductionMode, decimal deductionValue)
    {
        if (deductionMode == EarningDeductionMode.Percentage)
        {
            var boundedPercentage = Math.Clamp(deductionValue, 0m, 100m);
            return NormalizeMoney(annualGrossSalary * (boundedPercentage / 100m));
        }

        return NormalizeMoney(Math.Min(Math.Max(deductionValue, 0m), annualGrossSalary));
    }

    private static void ValidateModel(EarningUpsertModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Label))
        {
            throw new ArgumentException("Label is required.");
        }

        if (model.AnnualGrossSalary <= 0m)
        {
            throw new ArgumentException("Annual gross salary must be greater than zero.");
        }

        if (model.DeductionMode == EarningDeductionMode.Percentage && (model.DeductionValue < 0m || model.DeductionValue > 100m))
        {
            throw new ArgumentException("Percentage deductions must be between 0 and 100.");
        }

        if (model.DeductionMode == EarningDeductionMode.Flat && model.DeductionValue < 0m)
        {
            throw new ArgumentException("Flat deductions cannot be negative.");
        }
    }

    private static string NormalizeLabel(string label)
    {
        return label.Trim();
    }

    private static decimal NormalizeStoredDeductionValue(EarningDeductionMode deductionMode, decimal deductionValue)
    {
        if (deductionMode == EarningDeductionMode.Percentage)
        {
            return NormalizeMoney(Math.Clamp(deductionValue, 0m, 100m));
        }

        return NormalizeMoney(Math.Max(deductionValue, 0m));
    }

    private static string NormalizeCurrencyCode(string currencyCode)
    {
        var trimmedCurrencyCode = currencyCode.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(trimmedCurrencyCode) ? "USD" : trimmedCurrencyCode;
    }

    private static decimal NormalizeMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}