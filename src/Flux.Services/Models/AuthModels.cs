namespace Flux.Services.Models;

public record RegisterRequest(string Username, string Password);

public record LoginRequest(string Username, string Password);

public record AuthResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, string Username);
