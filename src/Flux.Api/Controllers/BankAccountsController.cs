using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Flux.Data.Models;
using Flux.Services;
using Flux.Services.Models;

namespace Flux.Api.Controllers;

/// <summary>
/// API controller for managing bank accounts.
/// Provides CRUD operations for bank account resources.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.FreeMember)]
[Route("api/[controller]")]
[Produces("application/json")]
public class BankAccountsController : ControllerBase
{
    private readonly IBankAccountService _service;
    private readonly IAccountAnalyticsService _analyticsService;
    private readonly ILogger<BankAccountsController> _logger;

    /// <summary>
    /// Initializes a new instance of the BankAccountsController class.
    /// </summary>
    /// <param name="service">The bank account service.</param>
    /// <param name="analyticsService">The account analytics service.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when service, analyticsService, or logger is null.</exception>
    public BankAccountsController(
        IBankAccountService service,
        IAccountAnalyticsService analyticsService,
        ILogger<BankAccountsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all bank accounts.
    /// </summary>
    /// <returns>A list of all bank accounts.</returns>
    /// <response code="200">Returns the list of bank accounts.</response>
    /// <response code="500">Internal server error.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<BankAccount>>> GetAccounts()
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);

            _logger.LogInformation("Retrieving all bank accounts.");
            var accounts = await _service.GetAllAccountsAsync(userId, isAdministrator);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving all bank accounts.");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while retrieving accounts.", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets rate analytics across the current user's portfolio.
    /// </summary>
    /// <returns>Portfolio APR/APY analytics summary and per-account details.</returns>
    /// <response code="200">Returns portfolio analytics.</response>
    /// <response code="500">Internal server error.</response>
    [HttpGet("analytics/portfolio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PortfolioRateAnalyticsResponse>> GetPortfolioAnalytics()
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);

            _logger.LogInformation("Retrieving portfolio analytics.");
            var analytics = await _analyticsService.GetPortfolioAnalyticsAsync(userId, isAdministrator);
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving portfolio analytics.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving portfolio analytics.", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets rate analytics for a specific account.
    /// </summary>
    /// <param name="id">The bank account ID.</param>
    /// <returns>APR/APY analytics for the requested account.</returns>
    /// <response code="200">Returns account analytics.</response>
    /// <response code="400">Bad request (invalid ID format).</response>
    /// <response code="404">Bank account not found.</response>
    /// <response code="500">Internal server error.</response>
    [HttpGet("{id}/analytics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AccountRateAnalyticsResponse>> GetAccountAnalytics(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);

            if (id <= 0)
            {
                _logger.LogWarning("Invalid account ID provided for analytics: {AccountId}", id);
                return BadRequest(new { message = "Account ID must be a positive integer." });
            }

            _logger.LogInformation("Retrieving analytics for bank account with ID: {AccountId}", id);
            var analytics = await _analyticsService.GetAccountAnalyticsByIdAsync(id, userId, isAdministrator);

            if (analytics == null)
            {
                _logger.LogWarning("Bank account not found for analytics with ID: {AccountId}", id);
                return NotFound(new { message = $"Bank account with ID {id} not found." });
            }

            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving analytics for bank account with ID: {AccountId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving account analytics.", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a bank account by its ID.
    /// </summary>
    /// <param name="id">The bank account ID.</param>
    /// <returns>The requested bank account if found.</returns>
    /// <response code="200">Returns the bank account.</response>
    /// <response code="400">Bad request (invalid ID format).</response>
    /// <response code="404">Bank account not found.</response>
    /// <response code="500">Internal server error.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BankAccount>> GetAccount(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);

            if (id <= 0)
            {
                _logger.LogWarning("Invalid account ID provided: {AccountId}", id);
                return BadRequest(new { message = "Account ID must be a positive integer." });
            }

            _logger.LogInformation("Retrieving bank account with ID: {AccountId}", id);
            var account = await _service.GetAccountByIdAsync(id, userId, isAdministrator);

            if (account == null)
            {
                _logger.LogWarning("Bank account not found with ID: {AccountId}", id);
                return NotFound(new { message = $"Bank account with ID {id} not found." });
            }

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving bank account with ID: {AccountId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving the account.", error = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new bank account.
    /// </summary>
    /// <param name="account">The bank account to create.</param>
    /// <returns>The created bank account.</returns>
    /// <response code="201">Bank account created successfully.</response>
    /// <response code="400">Bad request (invalid data).</response>
    /// <response code="500">Internal server error.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BankAccount>> PostAccount([FromBody] BankAccount account)
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var username = User.Identity?.Name
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Username is missing from token." });
            }

            if (account == null)
            {
                _logger.LogWarning("Null bank account provided for creation.");
                return BadRequest(new { message = "Bank account data is required." });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for bank account creation.");
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Creating new bank account.");
            var createdAccount = await _service.CreateAccountAsync(account, userId, username);

            if (createdAccount == null)
            {
                _logger.LogError("Failed to create bank account.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Failed to create bank account." });
            }

            return CreatedAtAction(nameof(GetAccount), new { id = createdAccount.Id }, createdAccount);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred during bank account creation.");
            return BadRequest(new { message = "Validation error.", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating a new bank account.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the account.", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing bank account.
    /// </summary>
    /// <param name="id">The ID of the bank account to update.</param>
    /// <param name="account">The updated bank account data.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Bank account updated successfully.</response>
    /// <response code="400">Bad request (mismatched ID or invalid data).</response>
    /// <response code="404">Bank account not found.</response>
    /// <response code="500">Internal server error.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PutAccount(int id, [FromBody] BankAccount account)
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);

            if (id <= 0)
            {
                _logger.LogWarning("Invalid account ID provided for update: {AccountId}", id);
                return BadRequest(new { message = "Account ID must be a positive integer." });
            }

            if (account == null)
            {
                _logger.LogWarning("Null bank account data provided for update.");
                return BadRequest(new { message = "Bank account data is required." });
            }

            if (id != account.Id)
            {
                _logger.LogWarning("Account ID mismatch during update. URL ID: {UrlId}, Body ID: {BodyId}", id, account.Id);
                return BadRequest(new { message = "Account ID in URL does not match the account ID in the request body." });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for bank account update.");
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Updating bank account with ID: {AccountId}", id);
            var success = await _service.UpdateAccountAsync(id, account, userId, isAdministrator);

            if (!success)
            {
                _logger.LogWarning("Bank account not found for update with ID: {AccountId}", id);
                return NotFound(new { message = $"Bank account with ID {id} not found." });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred during bank account update.");
            return BadRequest(new { message = "Validation error.", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating bank account with ID: {AccountId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while updating the account.", error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a bank account.
    /// </summary>
    /// <param name="id">The ID of the bank account to delete.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Bank account deleted successfully.</response>
    /// <response code="400">Bad request (invalid ID format).</response>
    /// <response code="404">Bank account not found.</response>
    /// <response code="500">Internal server error.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);

            if (id <= 0)
            {
                _logger.LogWarning("Invalid account ID provided for deletion: {AccountId}", id);
                return BadRequest(new { message = "Account ID must be a positive integer." });
            }

            _logger.LogInformation("Deleting bank account with ID: {AccountId}", id);
            var success = await _service.DeleteAccountAsync(id, userId, isAdministrator);

            if (!success)
            {
                _logger.LogWarning("Bank account not found for deletion with ID: {AccountId}", id);
                return NotFound(new { message = $"Bank account with ID {id} not found." });
            }

            _logger.LogInformation("Successfully deleted bank account with ID: {AccountId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting bank account with ID: {AccountId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while deleting the account.", error = ex.Message });
        }
    }

    /// <summary>
    /// Imports bank accounts from a CSV or XLSX file.
    /// </summary>
    /// <param name="file">The file to import.</param>
    /// <param name="targetUserId">Optional target user ID for administrators.</param>
    /// <returns>Import result metadata.</returns>
    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BankAccountImportResult>> ImportAccounts([FromForm] IFormFile file, [FromQuery] Guid? targetUserId)
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var username = User.Identity?.Name
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName);
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized(new { message = "Username is missing from token." });
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "A non-empty .csv or .xlsx file is required." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);

            await using var stream = file.OpenReadStream();
            var result = await _service.ImportAccountsAsync(
                stream,
                file.FileName,
                userId,
                username,
                isAdministrator,
                targetUserId);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized import attempt.");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred while importing bank accounts.");
            return BadRequest(new { message = "Validation error.", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while importing bank accounts.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while importing accounts.", error = ex.Message });
        }
    }

    /// <summary>
    /// Exports bank accounts as CSV.
    /// </summary>
    /// <param name="targetUserId">Optional target user ID for administrators.</param>
    [HttpGet("export/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportCsv([FromQuery] Guid? targetUserId)
    {
        return await ExportByFormatAsync(BankAccountFileFormat.Csv, targetUserId);
    }

    /// <summary>
    /// Exports bank accounts as XLSX.
    /// </summary>
    /// <param name="targetUserId">Optional target user ID for administrators.</param>
    [HttpGet("export/xlsx")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportXlsx([FromQuery] Guid? targetUserId)
    {
        return await ExportByFormatAsync(BankAccountFileFormat.Xlsx, targetUserId);
    }

    /// <summary>
    /// Downloads CSV import template.
    /// </summary>
    [HttpGet("template/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DownloadCsvTemplate()
    {
        var bytes = _service.GetImportTemplate(BankAccountFileFormat.Csv);
        return File(bytes, "text/csv", "bank-accounts-template.csv");
    }

    /// <summary>
    /// Downloads XLSX import template.
    /// </summary>
    [HttpGet("template/xlsx")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DownloadXlsxTemplate()
    {
        var bytes = _service.GetImportTemplate(BankAccountFileFormat.Xlsx);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "bank-accounts-template.xlsx");
    }

    private async Task<IActionResult> ExportByFormatAsync(BankAccountFileFormat format, Guid? targetUserId)
    {
        try
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
            var bytes = await _service.ExportAccountsAsync(userId, isAdministrator, targetUserId, format);
            var dateSuffix = DateTime.UtcNow.ToString("yyyyMMdd");

            return format switch
            {
                BankAccountFileFormat.Csv => File(bytes, "text/csv", $"bank-accounts-{dateSuffix}.csv"),
                BankAccountFileFormat.Xlsx => File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"bank-accounts-{dateSuffix}.xlsx"),
                _ => BadRequest(new { message = "Unsupported export format." })
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized export attempt.");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred while exporting bank accounts.");
            return BadRequest(new { message = "Validation error.", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while exporting bank accounts.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while exporting accounts.", error = ex.Message });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }
}