using Flux.Services;
using Flux.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flux.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var tokenResponse = await _authService.RegisterAsync(request);
            return CreatedAtAction(nameof(Register), tokenResponse);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateSafeErrorPayload(ex, "registration request validation", "The registration request is invalid."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(CreateSafeErrorPayload(ex, "registration conflict", "Unable to complete registration at this time."));
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var tokenResponse = await _authService.LoginAsync(request);
            return Ok(tokenResponse);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateSafeErrorPayload(ex, "login request validation", "The login request is invalid."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(CreateSafeErrorPayload(ex, "login authorization", "The provided credentials are invalid."));
        }
    }

    private object CreateSafeErrorPayload(Exception exception, string operation, string clientMessage)
    {
        var correlationId = HttpContext.TraceIdentifier;
        _logger.LogWarning(
            exception,
            "Auth operation failed during {Operation}. CorrelationId: {CorrelationId}",
            operation,
            correlationId);

        return new
        {
            message = clientMessage,
            correlationId
        };
    }
}
