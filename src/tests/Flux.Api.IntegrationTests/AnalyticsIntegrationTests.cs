using System.Net;
using System.Net.Http.Json;
using Flux.Api.IntegrationTests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flux.Api.IntegrationTests;

public sealed class AnalyticsIntegrationTests : IClassFixture<OwnershipTestFixture>
{
    private readonly OwnershipTestFixture _fixture;

    public AnalyticsIntegrationTests(OwnershipTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PortfolioAnalytics_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/bankaccounts/analytics/portfolio");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PortfolioAnalytics_AsMember_ReturnsComputedCreditAndSavingsMetrics()
    {
        var createCreditCardResponse = await _fixture.MemberClient.PostAsJsonAsync("/api/bankaccounts", new
        {
            accountName = "Member Card",
            balance = 1200m,
            type = 2,
            creditCardAprPercent = 18.5m
        });
        createCreditCardResponse.EnsureSuccessStatusCode();

        var createSavingsResponse = await _fixture.MemberClient.PostAsJsonAsync("/api/bankaccounts", new
        {
            accountName = "Member HYSA",
            balance = 5000m,
            type = 1,
            savingsApyPercent = 5.25m
        });
        createSavingsResponse.EnsureSuccessStatusCode();

        var analyticsResponse = await _fixture.MemberClient.GetAsync("/api/bankaccounts/analytics/portfolio");
        analyticsResponse.EnsureSuccessStatusCode();

        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<PortfolioAnalyticsDto>();
        Assert.NotNull(analytics);

        Assert.True(analytics!.CreditCardSummary.CardCount >= 1);
        Assert.Contains(analytics.CreditCards, card => card.AccountName == "Member Card" && card.AprPercent == 18.5m);

        Assert.True(analytics.SavingsSummary.AccountCount >= 1);
        var memberSavings = Assert.Single(analytics.SavingsAccounts, account => account.AccountName == "Member HYSA");
        Assert.Equal(5.25m, memberSavings.ApyPercent);
        Assert.Equal(3, memberSavings.CompoundingScenarios.Count);
    }

    [Fact]
    public async Task AccountAnalytics_AsNonOwner_ReturnsNotFound()
    {
        var response = await _fixture.MemberClient.GetAsync($"/api/bankaccounts/{_fixture.AdminAccountId}/analytics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AccountAnalytics_AsAdmin_ReturnsOkForMemberAccount()
    {
        var memberSavingsResponse = await _fixture.MemberClient.PostAsJsonAsync("/api/bankaccounts", new
        {
            accountName = "Member Savings For Admin Read",
            balance = 2300m,
            type = 1,
            savingsApyPercent = 5.0m
        });
        memberSavingsResponse.EnsureSuccessStatusCode();

        var memberAccount = await memberSavingsResponse.Content.ReadFromJsonAsync<AccountCreateDto>();
        Assert.NotNull(memberAccount);

        var analyticsResponse = await _fixture.AdminClient.GetAsync($"/api/bankaccounts/{memberAccount!.Id}/analytics");
        analyticsResponse.EnsureSuccessStatusCode();

        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<AccountAnalyticsDto>();
        Assert.NotNull(analytics);
        Assert.Equal(memberAccount.Id, analytics!.AccountId);
        Assert.Equal(1, (int)analytics.AccountType);
        Assert.NotNull(analytics.Savings);
    }

    private sealed record AccountCreateDto(int Id);

    private sealed record PortfolioAnalyticsDto(
        List<CreditCardAnalyticsDto> CreditCards,
        CreditCardSummaryDto CreditCardSummary,
        List<SavingsAnalyticsDto> SavingsAccounts,
        SavingsSummaryDto SavingsSummary);

    private sealed record CreditCardSummaryDto(
        int CardCount,
        decimal TotalBalance,
        decimal AverageAprPercent,
        decimal TotalEstimatedMonthlyInterest);

    private sealed record SavingsSummaryDto(
        int AccountCount,
        decimal TotalBalance,
        decimal AverageApyPercent,
        decimal TotalProjectedMonthlyInterest,
        decimal TotalProjectedAnnualInterest);

    private sealed record CreditCardAnalyticsDto(
        int AccountId,
        string AccountName,
        decimal Balance,
        decimal AprPercent,
        decimal EffectiveDailyRatePercent,
        decimal EstimatedMonthlyInterest,
        decimal MinimumPaymentAmount,
        int? EstimatedPayoffMonths,
        int AprRank);

    private sealed record SavingsAnalyticsDto(
        int AccountId,
        string AccountName,
        decimal Balance,
        decimal ApyPercent,
        decimal ProjectedMonthlyInterest,
        decimal ProjectedAnnualInterest,
        List<CompoundingProjectionDto> CompoundingScenarios,
        int ApyRank);

    private sealed record CompoundingProjectionDto(
        string Name,
        int PeriodsPerYear,
        decimal AnnualInterestEarned,
        decimal EndingBalance);

    private sealed record AccountAnalyticsDto(
        int AccountId,
        string AccountName,
        AccountTypeDto AccountType,
        CreditCardAnalyticsDto? CreditCard,
        SavingsAnalyticsDto? Savings);

    private enum AccountTypeDto
    {
        Checking = 0,
        Savings = 1,
        CreditCard = 2
    }
}
