using Flux.Services.Models;
using Flux.Data.Models;

namespace Flux.Services;

public interface IAuthService
{
    /// <summary>
    /// Registers a new user account with the provided credentials.
    /// </summary>
    /// <param name="request">The registration request containing username and password.</param>
    /// <returns>An AuthResponse containing the JWT token and user information.</returns>
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Authenticates a user with the provided credentials.
    /// </summary>
    /// <param name="request">The login request containing username and password.</param>
    /// <returns>An AuthResponse containing the JWT token and user information if authentication is successful.</returns>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Verifies the provided password against the stored password hash for a user.
    /// </summary>
    /// <param name="password">The plain text password to verify.</param>
    /// <param name="user">The user account to verify against.</param>
    /// <returns>True if the password is valid, otherwise false.</returns>
    bool VerifyPassword(string password, UserAccount user);
}
