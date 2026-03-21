using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flux.Api.IntegrationTests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flux.Api.IntegrationTests;

public sealed class AuthAndAccountsIntegrationTests : IClassFixture<FluxApiFactory>
{
    private readonly FluxApiFactory _factory;

    public AuthAndAccountsIntegrationTests(FluxApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/bankaccounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterThenCreateAndReadAccount_WithBearerToken_WorksEndToEnd()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "integration-user",
            password = "Password123"
        });

        registerResponse.EnsureSuccessStatusCode();

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var createResponse = await client.PostAsJsonAsync("/api/bankaccounts", new
        {
            accountName = "Primary",
            balance = 250.5m,
            type = 0
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/bankaccounts");
        listResponse.EnsureSuccessStatusCode();

        var accounts = await listResponse.Content.ReadFromJsonAsync<List<BankAccountDto>>();
        Assert.NotNull(accounts);
        Assert.Single(accounts);
        Assert.Equal("Primary", accounts[0].AccountName);
    }

    private sealed record AuthResponseDto(
        string AccessToken,
        string TokenType,
        DateTime ExpiresAtUtc,
        string Username,
        string Role);

    private sealed record BankAccountDto(
        Guid Id,
        Guid OwnerUserId,
        string AccountName,
        string Owner,
        decimal Balance,
        int Type,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
