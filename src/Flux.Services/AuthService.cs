using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Flux.Data;
using Flux.Data.Models;
using Flux.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Flux.Services;

public class AuthService : IAuthService
{
    private const int DefaultIterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly BankDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(BankDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var normalizedUsername = request.Username.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Username and password are required.");
        }

        if (request.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters long.");
        }

        var usernameExists = await _context.UserAccounts
            .AnyAsync(user => user.Username == normalizedUsername);

        if (usernameExists)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var hasExistingUsers = await _context.UserAccounts.AnyAsync();
        var assignedRole = hasExistingUsers ? ApplicationRoles.FreeMember : ApplicationRoles.Administrator;

        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            request.Password,
            saltBytes,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            HashSize);

        var userAccount = new UserAccount
        {
            Username = normalizedUsername,
            Role = assignedRole,
            PasswordSalt = Convert.ToBase64String(saltBytes),
            PasswordHash = Convert.ToBase64String(hashBytes),
            PasswordIterations = DefaultIterations
        };

        _context.UserAccounts.Add(userAccount);
        await _context.SaveChangesAsync();

        return CreateAuthResponse(userAccount);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var normalizedUsername = request.Username.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Username and password are required.");
        }

        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(account => account.Username == normalizedUsername);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        var isPasswordValid = VerifyPassword(request.Password, user);
        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            user.Role = ApplicationRoles.FreeMember;
            await _context.SaveChangesAsync();
        }

        return CreateAuthResponse(user);
    }

    public bool VerifyPassword(string password, UserAccount user)
    {
        var saltBytes = Convert.FromBase64String(user.PasswordSalt);
        var expectedHash = Convert.FromBase64String(user.PasswordHash);

        var passwordHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            user.PasswordIterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(passwordHash, expectedHash);
    }

    private AuthResponse CreateAuthResponse(UserAccount user)
    {
        var issuer = _configuration["Jwt:Issuer"]!;
        var audience = _configuration["Jwt:Audience"]!;
        var key = _configuration["Jwt:Key"]!;

        var expiresAtUtc = DateTime.UtcNow.AddHours(1);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse(
            AccessToken: serializedToken,
            TokenType: "Bearer",
            ExpiresAtUtc: expiresAtUtc,
            Username: user.Username,
            Role: user.Role);
    }
}
