using System.Net.Http.Json;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flux.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (UseRemoteAuthService())
        {
            return await ProxyToAuthServiceAsync("register", request);
        }

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
        if (UseRemoteAuthService())
        {
            return await ProxyToAuthServiceAsync("login", request);
        }

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

    private bool UseRemoteAuthService()
    {
        var baseUrl = _configuration["AuthService:BaseUrl"];
        return !string.IsNullOrWhiteSpace(baseUrl);
    }

    private async Task<ActionResult<AuthResponse>> ProxyToAuthServiceAsync<TRequest>(string action, TRequest request)
    {
        var baseUrl = _configuration["AuthService:BaseUrl"]!;
        var client = _httpClientFactory.CreateClient();
        var endpoint = $"{baseUrl.TrimEnd('/')}/api/auth/{action}";

        var response = await client.PostAsJsonAsync(endpoint, request);
        var payload = await response.Content.ReadAsStringAsync();

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = payload,
            ContentType = "application/json"
        };
    }
}
