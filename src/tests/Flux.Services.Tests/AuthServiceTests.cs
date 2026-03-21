using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Flux.Data;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Flux.Services.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankDbContext _context;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BankDbContext(options);
        _context.Database.EnsureCreated();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-super-secret-jwt-key-with-enough-length-123456",
                ["Jwt:Issuer"] = "Flux.Tests",
                ["Jwt:Audience"] = "Flux.Tests.Client"
            })
            .Build();

        _service = new AuthService(_context, configuration);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RegisterAsync_FirstUser_AssignsAdministratorRole()
    {
        var response = await _service.RegisterAsync(new RegisterRequest("admin", "Password123"));

        Assert.Equal(ApplicationRoles.Administrator, response.Role);
    }

    [Fact]
    public async Task RegisterAsync_SecondUser_AssignsFreeMemberRole()
    {
        await _service.RegisterAsync(new RegisterRequest("admin", "Password123"));

        var response = await _service.RegisterAsync(new RegisterRequest("member", "Password123"));

        Assert.Equal(ApplicationRoles.FreeMember, response.Role);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUsername_ThrowsInvalidOperationException()
    {
        await _service.RegisterAsync(new RegisterRequest("dup", "Password123"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegisterAsync(new RegisterRequest("dup", "Password123")));
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        await _service.RegisterAsync(new RegisterRequest("user1", "Password123"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.LoginAsync(new LoginRequest("user1", "WrongPassword")));
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsJwtWithExpectedClaims()
    {
        var registerResponse = await _service.RegisterAsync(new RegisterRequest("claim-user", "Password123"));

        var loginResponse = await _service.LoginAsync(new LoginRequest("claim-user", "Password123"));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(loginResponse.AccessToken);
        Assert.Equal(registerResponse.Username, loginResponse.Username);
        Assert.Equal("Bearer", loginResponse.TokenType);
        Assert.Equal("claim-user", token.Claims.First(c => c.Type == "unique_name").Value);
        Assert.Equal("Flux.Tests", token.Issuer);
        Assert.Contains("Flux.Tests.Client", token.Audiences);
    }

    [Fact]
    public async Task VerifyPassword_ReturnsTrueForCorrectPassword_AndFalseForWrongPassword()
    {
        await _service.RegisterAsync(new RegisterRequest("verify-user", "Password123"));
        var user = await _context.UserAccounts.SingleAsync(x => x.Username == "verify-user");

        var valid = _service.VerifyPassword("Password123", user);
        var invalid = _service.VerifyPassword("NotThePassword", user);

        Assert.True(valid);
        Assert.False(invalid);
    }

    [Fact]
    public async Task RegisterAsync_TrimsUsernameBeforePersisting()
    {
        await _service.RegisterAsync(new RegisterRequest("  spaced-user  ", "Password123"));
        var user = await _context.UserAccounts.SingleAsync();

        Assert.Equal("spaced-user", user.Username);
    }
}
