using Flux.Data;
using Flux.Data.Models;
using Flux.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Flux.Services;

public sealed class AccountAnalyticsService : IAccountAnalyticsService
{
    private readonly BankDbContext _context;
    private readonly RateAnalyticsOptions _options;

    public AccountAnalyticsService(BankDbContext context, IOptions<RateAnalyticsOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task<PortfolioRateAnalyticsResponse> GetPortfolioAnalyticsAsync(Guid userId, bool isAdministrator)
    {
        var accountsQuery = _context.Accounts.AsNoTracking();
        if (!isAdministrator)
        {
            accountsQuery = accountsQuery.Where(account => account.OwnerUserId == userId);
        }

        var accounts = await accountsQuery.ToListAsync();

        var creditCards = BuildCreditCardAnalytics(accounts.Where(account => account.Type == AccountType.CreditCard));
        var savingsAccounts = BuildSavingsAnalytics(accounts.Where(account => account.Type == AccountType.Savings));

        var creditCardSummary = new CreditCardPortfolioSummary(
            CardCount: creditCards.Count,
            TotalBalance: RoundMoney(creditCards.Sum(card => card.Balance)),
            AverageAprPercent: creditCards.Count == 0 ? 0m : RoundRate(creditCards.Average(card => card.AprPercent)),
            TotalEstimatedMonthlyInterest: RoundMoney(creditCards.Sum(card => card.EstimatedMonthlyInterest)));

        var savingsSummary = new SavingsPortfolioSummary(
            AccountCount: savingsAccounts.Count,
            TotalBalance: RoundMoney(savingsAccounts.Sum(account => account.Balance)),
            AverageApyPercent: savingsAccounts.Count == 0 ? 0m : RoundRate(savingsAccounts.Average(account => account.ApyPercent)),
            TotalProjectedMonthlyInterest: RoundMoney(savingsAccounts.Sum(account => account.ProjectedMonthlyInterest)),
            TotalProjectedAnnualInterest: RoundMoney(savingsAccounts.Sum(account => account.ProjectedAnnualInterest)));

        return new PortfolioRateAnalyticsResponse(
            CreditCards: creditCards,
            CreditCardSummary: creditCardSummary,
            SavingsAccounts: savingsAccounts,
            SavingsSummary: savingsSummary);
    }

    public async Task<AccountRateAnalyticsResponse?> GetAccountAnalyticsByIdAsync(Guid accountId, Guid userId, bool isAdministrator)
    {
        var account = await _context.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId);
        if (account is null)
        {
            return null;
        }

        if (!isAdministrator && account.OwnerUserId != userId)
        {
            return null;
        }

        return account.Type switch
        {
            AccountType.CreditCard => new AccountRateAnalyticsResponse(
                AccountId: account.Id,
                AccountName: ResolveAccountName(account),
                AccountType: account.Type,
                CreditCard: BuildCreditCardAnalytics([account]).Single(),
                Savings: null),
            AccountType.Savings => new AccountRateAnalyticsResponse(
                AccountId: account.Id,
                AccountName: ResolveAccountName(account),
                AccountType: account.Type,
                CreditCard: null,
                Savings: BuildSavingsAnalytics([account]).Single()),
            _ => new AccountRateAnalyticsResponse(
                AccountId: account.Id,
                AccountName: ResolveAccountName(account),
                AccountType: account.Type,
                CreditCard: null,
                Savings: null)
        };
    }

    private List<CreditCardRateAnalytics> BuildCreditCardAnalytics(IEnumerable<BankAccount> cards)
    {
        var rankedCards = cards
            .Select(card =>
            {
                var aprPercent = ResolveAprPercent(card);
                var boundedBalance = Math.Max(0m, card.Balance);
                var monthlyRate = aprPercent / 100m / 12m;
                var effectiveDailyRatePercent = aprPercent / 365m;
                var estimatedMonthlyInterest = boundedBalance * monthlyRate;

                var minimumPaymentByPercent = boundedBalance * (_options.CreditCard.MinimumPaymentPercent / 100m);
                var minimumPaymentAmount = boundedBalance <= 0m
                    ? 0m
                    : Math.Min(boundedBalance, Math.Max(minimumPaymentByPercent, _options.CreditCard.MinimumPaymentFlatAmount));

                return new
                {
                    Account = card,
                    Analytics = new CreditCardRateAnalytics(
                        AccountId: card.Id,
                        AccountName: ResolveAccountName(card),
                        Balance: RoundMoney(boundedBalance),
                        AprPercent: RoundRate(aprPercent),
                        EffectiveDailyRatePercent: RoundRate(effectiveDailyRatePercent),
                        EstimatedMonthlyInterest: RoundMoney(estimatedMonthlyInterest),
                        MinimumPaymentAmount: RoundMoney(minimumPaymentAmount),
                        EstimatedPayoffMonths: EstimatePayoffMonths(boundedBalance, minimumPaymentAmount, monthlyRate),
                        AprRank: 0)
                };
            })
            .OrderBy(item => item.Analytics.AprPercent)
            .ThenByDescending(item => item.Analytics.Balance)
            .ToList();

        return rankedCards
            .Select((item, index) => item.Analytics with { AprRank = index + 1 })
            .ToList();
    }

    private List<SavingsRateAnalytics> BuildSavingsAnalytics(IEnumerable<BankAccount> savingsAccounts)
    {
        var rankedSavings = savingsAccounts
            .Select(account =>
            {
                var apyPercent = ResolveApyPercent(account);
                var boundedBalance = Math.Max(0m, account.Balance);
                var projectedMonthlyInterest = boundedBalance * ((decimal)Math.Pow(1 + (double)(apyPercent / 100m), 1d / 12d) - 1m);
                var projectedAnnualInterest = boundedBalance * (apyPercent / 100m);

                var compoundingScenarios = new List<CompoundingProjection>
                {
                    BuildCompoundingProjection("Daily", 365, boundedBalance, apyPercent),
                    BuildCompoundingProjection("Monthly", 12, boundedBalance, apyPercent),
                    BuildCompoundingProjection("Quarterly", 4, boundedBalance, apyPercent)
                };

                return new SavingsRateAnalytics(
                    AccountId: account.Id,
                    AccountName: ResolveAccountName(account),
                    Balance: RoundMoney(boundedBalance),
                    ApyPercent: RoundRate(apyPercent),
                    ProjectedMonthlyInterest: RoundMoney(projectedMonthlyInterest),
                    ProjectedAnnualInterest: RoundMoney(projectedAnnualInterest),
                    CompoundingScenarios: compoundingScenarios,
                    ApyRank: 0);
            })
            .OrderByDescending(item => item.ApyPercent)
            .ThenByDescending(item => item.Balance)
            .ToList();

        return rankedSavings
            .Select((item, index) => item with { ApyRank = index + 1 })
            .ToList();
    }

    private static int? EstimatePayoffMonths(decimal balance, decimal minimumPaymentAmount, decimal monthlyRate)
    {
        if (balance <= 0m || minimumPaymentAmount <= 0m)
        {
            return 0;
        }

        if (monthlyRate <= 0m)
        {
            return (int)Math.Ceiling(balance / minimumPaymentAmount);
        }

        var interestOnlyPayment = balance * monthlyRate;
        if (minimumPaymentAmount <= interestOnlyPayment)
        {
            return null;
        }

        var ratio = (double)(minimumPaymentAmount / (minimumPaymentAmount - balance * monthlyRate));
        var growthFactor = 1d + (double)monthlyRate;
        var months = Math.Ceiling(Math.Log(ratio) / Math.Log(growthFactor));

        return months > int.MaxValue ? int.MaxValue : (int)months;
    }

    private CompoundingProjection BuildCompoundingProjection(string name, int periodsPerYear, decimal balance, decimal apyPercent)
    {
        var ratePerPeriod = apyPercent / 100m / periodsPerYear;
        var endingBalance = balance * (decimal)Math.Pow((double)(1 + ratePerPeriod), periodsPerYear);
        var annualInterest = endingBalance - balance;

        return new CompoundingProjection(
            Name: name,
            PeriodsPerYear: periodsPerYear,
            AnnualInterestEarned: RoundMoney(annualInterest),
            EndingBalance: RoundMoney(endingBalance));
    }

    private decimal ResolveAprPercent(BankAccount account)
    {
        return account.CreditCardAprPercent ?? _options.CreditCard.AprPercent;
    }

    private decimal ResolveApyPercent(BankAccount account)
    {
        return account.SavingsApyPercent ?? _options.Savings.ApyPercent;
    }

    private static string ResolveAccountName(BankAccount account)
    {
        var accountName = account.AccountName?.Trim();
        return string.IsNullOrWhiteSpace(accountName)
            ? account.Owner.Trim()
            : accountName;
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}