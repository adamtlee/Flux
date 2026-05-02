using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flux.Api.IntegrationTests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flux.Api.IntegrationTests;

public sealed class EarningsIntegrationTests : IClassFixture<FluxApiFactory>
{
    private readonly FluxApiFactory _factory;

    public EarningsIntegrationTests(FluxApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateUpdateDeleteEarning_WithBearerToken_WorksEndToEnd()
    {
        using var client = CreateClient();

        var auth = await RegisterAsync(client, "earn-member", "Password123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var createResponse = await client.PostAsJsonAsync("/api/earnings", new
        {
            label = "Primary Job",
            annualGrossSalary = 50000m,
            deductionMode = 0,
            deductionValue = 20m,
            currencyCode = "USD"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<EarningDto>();
        Assert.NotNull(created);

        var listResponse = await client.GetAsync("/api/earnings");
        listResponse.EnsureSuccessStatusCode();
        var earnings = await listResponse.Content.ReadFromJsonAsync<List<EarningDto>>();
        Assert.NotNull(earnings);
        Assert.Single(earnings);

        var updateResponse = await client.PutAsJsonAsync($"/api/earnings/{created!.Id}", new
        {
            label = "Primary Role",
            annualGrossSalary = 55000m,
            deductionMode = 1,
            deductionValue = 5000m,
            currencyCode = "USD"
        });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await client.GetFromJsonAsync<EarningDto>($"/api/earnings/{created.Id}");
        Assert.NotNull(updated);
        Assert.Equal("Primary Role", updated!.Label);

        var deleteResponse = await client.DeleteAsync($"/api/earnings/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDeleteResponse = await client.GetAsync("/api/earnings");
        afterDeleteResponse.EnsureSuccessStatusCode();
        var remaining = await afterDeleteResponse.Content.ReadFromJsonAsync<List<EarningDto>>();
        Assert.NotNull(remaining);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task NonOwnerCannotReadEarningById_Returns404()
    {
        using var ownerClient = CreateClient();
        using var otherClient = CreateClient();

        var ownerAuth = await RegisterAsync(ownerClient, "earn-owner", "Password123!");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAuth.AccessToken);

        var otherAuth = await RegisterAsync(otherClient, "earn-other", "Password123!");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherAuth.AccessToken);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/earnings", new
        {
            label = "Private Job",
            annualGrossSalary = 42000m,
            deductionMode = 0,
            deductionValue = 18m,
            currencyCode = "USD"
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<EarningDto>();
        Assert.NotNull(created);

        var unauthorizedGet = await otherClient.GetAsync($"/api/earnings/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedGet.StatusCode);
    }

    [Fact]
    public async Task SummaryEndpoint_ReturnsCombinedAndPerEntryBreakdowns()
    {
        using var client = CreateClient();

        var auth = await RegisterAsync(client, "earn-summary", "Password123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        await client.PostAsJsonAsync("/api/earnings", new
        {
            label = "Primary Job",
            annualGrossSalary = 50000m,
            deductionMode = 0,
            deductionValue = 20m,
            currencyCode = "USD"
        });

        await client.PostAsJsonAsync("/api/earnings", new
        {
            label = "Weekend Contract",
            annualGrossSalary = 10000m,
            deductionMode = 1,
            deductionValue = 1000m,
            currencyCode = "USD"
        });

        var summaryResponse = await client.GetAsync("/api/earnings/summary");
        summaryResponse.EnsureSuccessStatusCode();

        var summary = await summaryResponse.Content.ReadFromJsonAsync<EarningsSummaryDto>();

        Assert.NotNull(summary);
        Assert.Equal(2, summary!.Entries.Count);
        Assert.Equal(60000m, summary.TotalGross.Annual);
        Assert.Equal(49000m, summary.TotalNet.Annual);
        Assert.Equal(2307.69m, summary.TotalGross.BiWeekly);
        Assert.Equal(4083.33m, summary.TotalNet.Monthly);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    private static async Task<AuthResponseDto> RegisterAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return auth!;
    }

    private sealed record AuthResponseDto(
        string AccessToken,
        string TokenType,
        DateTime ExpiresAtUtc,
        string Username,
        string Role);

    private sealed record EarningDto(
        int Id,
        Guid OwnerUserId,
        string OwnerUsername,
        string Label,
        decimal AnnualGrossSalary,
        int DeductionMode,
        decimal DeductionValue,
        string CurrencyCode,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record EarningsSummaryDto(
        IReadOnlyList<EarningSummaryEntryDto> Entries,
        EarningsPeriodBreakdownDto TotalGross,
        EarningsPeriodBreakdownDto TotalNet,
        decimal TotalAnnualDeductions);

    private sealed record EarningSummaryEntryDto(
        int Id,
        string Label,
        decimal AnnualGrossSalary,
        int DeductionMode,
        decimal DeductionValue,
        string CurrencyCode,
        decimal AnnualDeduction,
        decimal AnnualNetSalary,
        EarningsPeriodBreakdownDto GrossBreakdown,
        EarningsPeriodBreakdownDto NetBreakdown);

    private sealed record EarningsPeriodBreakdownDto(
        decimal Annual,
        decimal Monthly,
        decimal BiWeekly,
        decimal Weekly,
        decimal Daily,
        decimal Hourly);
}