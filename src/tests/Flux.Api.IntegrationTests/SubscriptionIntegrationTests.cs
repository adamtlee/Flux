using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flux.Api.IntegrationTests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flux.Api.IntegrationTests;

public sealed class SubscriptionIntegrationTests : IClassFixture<FluxApiFactory>
{
    private readonly FluxApiFactory _factory;

    public SubscriptionIntegrationTests(FluxApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateThenListSubscription_WithBearerToken_WorksEndToEnd()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var auth = await RegisterAsync(client, "sub-member", "Password123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var dueDate = DateTime.UtcNow.Date.AddDays(5);
        var createResponse = await client.PostAsJsonAsync("/api/subscriptions", new
        {
            serviceName = "Spotify",
            providerName = "Spotify",
            category = 0,
            tags = new[] { "music", "streaming" },
            billingCycle = 1,
            amount = 12.99m,
            currencyCode = "USD",
            startDateUtc = DateTime.UtcNow.Date.AddMonths(-1),
            nextDueDateUtc = dueDate,
            reminderDaysBeforeDue = 3,
            autoRenew = true,
            status = 0,
            notes = "Family plan"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/subscriptions");
        listResponse.EnsureSuccessStatusCode();

        var subscriptions = await listResponse.Content.ReadFromJsonAsync<List<SubscriptionDto>>();
        Assert.NotNull(subscriptions);
        Assert.Single(subscriptions);
        Assert.Equal("Spotify", subscriptions[0].ServiceName);
    }

    [Fact]
    public async Task NonOwnerCannotReadSubscriptionById_Returns404()
    {
        using var ownerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var otherClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var ownerAuth = await RegisterAsync(ownerClient, "sub-owner", "Password123!");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAuth.AccessToken);

        var otherAuth = await RegisterAsync(otherClient, "sub-other", "Password123!");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherAuth.AccessToken);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/subscriptions", new
        {
            serviceName = "Care Insurance",
            providerName = "Care Insurance",
            category = 1,
            tags = new[] { "insurance", "health" },
            billingCycle = 1,
            amount = 89m,
            currencyCode = "USD",
            startDateUtc = DateTime.UtcNow.Date.AddMonths(-2),
            nextDueDateUtc = DateTime.UtcNow.Date.AddDays(4),
            reminderDaysBeforeDue = 3,
            autoRenew = true,
            status = 0
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(created);

        var unauthorizedGet = await otherClient.GetAsync($"/api/subscriptions/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, unauthorizedGet.StatusCode);
    }

    [Fact]
    public async Task RemindersAndAnalyticsEndpoints_ReturnExpectedPayloads()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var auth = await RegisterAsync(client, "sub-analytics", "Password123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var createResponse = await client.PostAsJsonAsync("/api/subscriptions", new
        {
            serviceName = "Cell Phone",
            providerName = "Carrier",
            category = 3,
            tags = new[] { "phone", "utility" },
            billingCycle = 1,
            amount = 65m,
            currencyCode = "USD",
            startDateUtc = DateTime.UtcNow.Date.AddMonths(-3),
            nextDueDateUtc = DateTime.UtcNow.Date.AddDays(2),
            reminderDaysBeforeDue = 3,
            autoRenew = true,
            status = 0
        });
        createResponse.EnsureSuccessStatusCode();

        var remindersResponse = await client.GetAsync("/api/subscriptions/reminders?withinDays=7");
        remindersResponse.EnsureSuccessStatusCode();
        var reminders = await remindersResponse.Content.ReadFromJsonAsync<List<SubscriptionDto>>();
        Assert.NotNull(reminders);
        Assert.Single(reminders);

        var analyticsResponse = await client.GetAsync("/api/subscriptions/analytics/monthly?months=6");
        analyticsResponse.EnsureSuccessStatusCode();
        var analytics = await analyticsResponse.Content.ReadFromJsonAsync<AnalyticsDto>();

        Assert.NotNull(analytics);
        Assert.Equal(6, analytics!.Trend.Count);
        Assert.True(analytics.CurrentMonthlyEstimatedSpend > 0m);
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

    private sealed record SubscriptionDto(
        int Id,
        Guid OwnerUserId,
        string OwnerUsername,
        string ServiceName,
        string ProviderName,
        int Category,
        IReadOnlyList<string> Tags,
        int BillingCycle,
        decimal Amount,
        string CurrencyCode,
        DateTime StartDateUtc,
        DateTime NextDueDateUtc,
        int ReminderDaysBeforeDue,
        bool AutoRenew,
        int Status,
        string? Notes,
        DateTime? CancelledAtUtc,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record AnalyticsDto(
        IReadOnlyList<TrendPointDto> Trend,
        IReadOnlyList<CategoryPointDto> CategoryBreakdown,
        decimal CurrentMonthlyEstimatedSpend);

    private sealed record TrendPointDto(int Year, int Month, decimal EstimatedSpend);

    private sealed record CategoryPointDto(int Category, decimal EstimatedMonthlySpend, int SubscriptionCount);
}
