namespace Flux.Services.Models;

public record RegisterRequest(string Username, string Password);

public record LoginRequest(string Username, string Password);

public record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, string Username, string Role);

public static class ApplicationRoles
{
	public const string Administrator = "Administrator";
	public const string PremiumMember = "PremiumMember";
	public const string FreeMember = "FreeMember";
}

public static class AuthorizationPolicies
{
	public const string FreeMember = "FreeMemberPolicy";
	public const string PremiumMember = "PremiumMemberPolicy";
	public const string Administrator = "AdministratorPolicy";
}
