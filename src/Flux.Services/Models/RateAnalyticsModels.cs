using Flux.Data.Models;

namespace Flux.Services.Models;

public sealed record PortfolioRateAnalyticsResponse(
    IReadOnlyList<CreditCardRateAnalytics> CreditCards,
    CreditCardPortfolioSummary CreditCardSummary,
    IReadOnlyList<SavingsRateAnalytics> SavingsAccounts,
    SavingsPortfolioSummary SavingsSummary);

public sealed record CreditCardPortfolioSummary(
    int CardCount,
    decimal TotalBalance,
    decimal AverageAprPercent,
    decimal TotalEstimatedMonthlyInterest);

public sealed record SavingsPortfolioSummary(
    int AccountCount,
    decimal TotalBalance,
    decimal AverageApyPercent,
    decimal TotalProjectedMonthlyInterest,
    decimal TotalProjectedAnnualInterest);

public sealed record CreditCardRateAnalytics(
    Guid AccountId,
    string AccountName,
    decimal Balance,
    decimal AprPercent,
    decimal EffectiveDailyRatePercent,
    decimal EstimatedMonthlyInterest,
    decimal MinimumPaymentAmount,
    int? EstimatedPayoffMonths,
    int AprRank);

public sealed record SavingsRateAnalytics(
    Guid AccountId,
    string AccountName,
    decimal Balance,
    decimal ApyPercent,
    decimal ProjectedMonthlyInterest,
    decimal ProjectedAnnualInterest,
    IReadOnlyList<CompoundingProjection> CompoundingScenarios,
    int ApyRank);

public sealed record CompoundingProjection(
    string Name,
    int PeriodsPerYear,
    decimal AnnualInterestEarned,
    decimal EndingBalance);

public sealed record AccountRateAnalyticsResponse(
    Guid AccountId,
    string AccountName,
    AccountType AccountType,
    CreditCardRateAnalytics? CreditCard,
    SavingsRateAnalytics? Savings);