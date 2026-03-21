using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flux.Api.IntegrationTests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flux.Api.IntegrationTests;

/// <summary>
/// Shared test fixture that registers an admin and a member user, creates one account per
/// user, and exposes pre-authenticated HTTP clients for use across all ownership tests.
/// </summary>
public sealed class OwnershipTestFixture : IAsyncLifetime
{
    public FluxApiFactory Factory { get; private set; } = null!;
    public HttpClient AdminClient { get; private set; } = null!;
    public HttpClient MemberClient { get; private set; } = null!;
    public Guid AdminAccountId { get; private set; }
    public Guid MemberAccountId { get; private set; }

    public async Task InitializeAsync()
    {
        Factory = new FluxApiFactory();

        var clientOptions = new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        };

        AdminClient = Factory.CreateClient(clientOptions);
        MemberClient = Factory.CreateClient(clientOptions);

        // First registration → Administrator role
        var adminToken = await RegisterAsync(AdminClient, "owner-admin", "Password123!");
        AdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Second registration → FreeMember role
        var memberToken = await RegisterAsync(MemberClient, "owner-member", "Password123!");
        MemberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        // Seed one account per user so tests have stable targets
        AdminAccountId = await CreateAccountAsync(AdminClient, "Admin Checking");
        MemberAccountId = await CreateAccountAsync(MemberClient, "Member Savings");
    }

    public async Task DisposeAsync()
    {
        AdminClient.Dispose();
        MemberClient.Dispose();
        await Factory.DisposeAsync();
    }

    private static async Task<string> RegisterAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AuthDto>();
        return dto!.AccessToken;
    }

    internal static async Task<Guid> CreateAccountAsync(HttpClient client, string accountName)
    {
        var response = await client.PostAsJsonAsync("/api/bankaccounts", new
        {
            accountName,
            balance = 100m,
            type = 0
        });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!.Id;
    }

    internal sealed record AuthDto(string AccessToken, string TokenType, DateTime ExpiresAtUtc, string Username, string Role);
    internal sealed record AccountDto(Guid Id, Guid OwnerUserId, string AccountName, string Owner, decimal Balance, int Type, DateTime CreatedAt, DateTime UpdatedAt);
}

/// <summary>
/// Integration tests covering ownership and administrator access-control behaviour
/// for GET /api/bankaccounts/{id}, PUT /api/bankaccounts/{id}, and DELETE /api/bankaccounts/{id}.
/// </summary>
public sealed class AccountOwnershipIntegrationTests : IClassFixture<OwnershipTestFixture>
{
    private readonly OwnershipTestFixture _fixture;

    public AccountOwnershipIntegrationTests(OwnershipTestFixture fixture)
    {
        _fixture = fixture;
    }

    // ── GET by id ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_AsOwner_Returns200()
    {
        var response = await _fixture.AdminClient.GetAsync($"/api/bankaccounts/{_fixture.AdminAccountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsNonOwner_Returns404()
    {
        // Member cannot see admin's account
        var response = await _fixture.MemberClient.GetAsync($"/api/bankaccounts/{_fixture.AdminAccountId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsAdmin_CanAccessAnyAccount_Returns200()
    {
        // Admin can see member's account
        var response = await _fixture.AdminClient.GetAsync($"/api/bankaccounts/{_fixture.MemberAccountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithEmptyGuid_Returns400()
    {
        var response = await _fixture.AdminClient.GetAsync($"/api/bankaccounts/{Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── PUT (update) ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AsNonOwner_Returns404()
    {
        // Member cannot update admin's account; service returns false → controller returns 404
        var response = await _fixture.MemberClient.PutAsJsonAsync(
            $"/api/bankaccounts/{_fixture.AdminAccountId}",
            new
            {
                id = _fixture.AdminAccountId,
                accountName = "Should Not Update",
                owner = "owner-member",
                ownerUserId = Guid.NewGuid(),
                balance = 9999m,
                type = 0,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_CanUpdateAnyAccount_Returns204()
    {
        // Create a fresh member-owned account to avoid interfering with shared fixture state
        var tempId = await OwnershipTestFixture.CreateAccountAsync(_fixture.MemberClient, "Temp-For-Update");

        var response = await _fixture.AdminClient.PutAsJsonAsync(
            $"/api/bankaccounts/{tempId}",
            new
            {
                id = tempId,
                accountName = "Updated By Admin",
                owner = "owner-member",
                ownerUserId = Guid.NewGuid(),
                balance = 500m,
                type = 1,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── DELETE ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_AsNonOwner_Returns404()
    {
        // Member cannot delete admin's account; no mutation occurs
        var response = await _fixture.MemberClient.DeleteAsync($"/api/bankaccounts/{_fixture.AdminAccountId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_CanDeleteAnyAccount_Returns204()
    {
        // Create a fresh member-owned account for this destructive test
        var tempId = await OwnershipTestFixture.CreateAccountAsync(_fixture.MemberClient, "Temp-For-Delete");

        var response = await _fixture.AdminClient.DeleteAsync($"/api/bankaccounts/{tempId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── Auth edge cases ────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithBadCredentials_Returns401()
    {
        using var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "owner-admin",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_Returns409()
    {
        using var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        // "owner-admin" was registered by the fixture; registering again must conflict
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "owner-admin",
            password = "AnotherPassword123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
