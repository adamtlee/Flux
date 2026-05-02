using Flux.Data.Models;

namespace Flux.Services.Models;

public sealed record EarningUpsertModel(
    string Label,
    decimal AnnualGrossSalary,
    EarningDeductionMode DeductionMode,
    decimal DeductionValue,
    string CurrencyCode
);

public sealed record EarningsPeriodBreakdown(
    decimal Annual,
    decimal Monthly,
    decimal BiWeekly,
    decimal Weekly,
    decimal Daily,
    decimal Hourly
);

public sealed record EarningSummaryEntry(
    int Id,
    string Label,
    decimal AnnualGrossSalary,
    EarningDeductionMode DeductionMode,
    decimal DeductionValue,
    string CurrencyCode,
    decimal AnnualDeduction,
    decimal AnnualNetSalary,
    EarningsPeriodBreakdown GrossBreakdown,
    EarningsPeriodBreakdown NetBreakdown
);

public sealed record EarningsSummaryResponse(
    IReadOnlyList<EarningSummaryEntry> Entries,
    EarningsPeriodBreakdown TotalGross,
    EarningsPeriodBreakdown TotalNet,
    decimal TotalAnnualDeductions
);