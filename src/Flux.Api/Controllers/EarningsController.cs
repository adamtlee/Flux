using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Flux.Api.Contracts;
using Flux.Data.Models;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flux.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.FreeMember)]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class EarningsController(
    IEarningsService earningsService,
    ILogger<EarningsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<EarningResponseDto>>> GetEarnings()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var earnings = await earningsService.GetEarningsAsync(userId, isAdministrator);
        return Ok(earnings.Select(MapToResponseDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EarningResponseDto>> GetEarningById(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Earning ID must be a positive integer." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var earning = await earningsService.GetEarningByIdAsync(id, userId, isAdministrator);
        if (earning is null)
        {
            return NotFound(new { message = $"Earning with ID {id} not found." });
        }

        return Ok(MapToResponseDto(earning));
    }

    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EarningsSummaryResponseDto>> GetSummary()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var summary = await earningsService.GetSummaryAsync(userId, isAdministrator);
        return Ok(MapToResponseDto(summary));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EarningResponseDto>> CreateEarning([FromBody] EarningCreateRequestDto request)
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

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var created = await earningsService.CreateEarningAsync(userId, username, MapToModel(request));
            return CreatedAtAction(nameof(GetEarningById), new { id = created.Id }, MapToResponseDto(created));
        }
        catch (ArgumentException ex)
        {
            return CreateValidationErrorResponse(ex, "earning creation");
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateEarning(int id, [FromBody] EarningUpdateRequestDto request)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Earning ID must be a positive integer." });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { message = "User identity is missing from token." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
            var updated = await earningsService.UpdateEarningAsync(id, userId, isAdministrator, MapToModel(request));
            if (updated is null)
            {
                return NotFound(new { message = $"Earning with ID {id} not found." });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return CreateValidationErrorResponse(ex, "earning update");
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteEarning(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Earning ID must be a positive integer." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var deleted = await earningsService.DeleteEarningAsync(id, userId, isAdministrator);
        if (!deleted)
        {
            return NotFound(new { message = $"Earning with ID {id} not found." });
        }

        return NoContent();
    }

    private BadRequestObjectResult CreateValidationErrorResponse(Exception exception, string operation)
    {
        var correlationId = HttpContext.TraceIdentifier;
        logger.LogWarning(
            exception,
            "Validation error during {Operation}. CorrelationId: {CorrelationId}",
            operation,
            correlationId);

        return BadRequest(new
        {
            message = "The request could not be processed due to invalid input.",
            correlationId
        });
    }

    private static EarningUpsertModel MapToModel(EarningCreateRequestDto request)
    {
        return new EarningUpsertModel(
            request.Label,
            request.AnnualGrossSalary,
            request.DeductionMode,
            request.DeductionValue,
            request.CurrencyCode);
    }

    private static EarningUpsertModel MapToModel(EarningUpdateRequestDto request)
    {
        return new EarningUpsertModel(
            request.Label,
            request.AnnualGrossSalary,
            request.DeductionMode,
            request.DeductionValue,
            request.CurrencyCode);
    }

    private static EarningResponseDto MapToResponseDto(Earning earning)
    {
        return new EarningResponseDto
        {
            Id = earning.Id,
            OwnerUserId = earning.OwnerUserId,
            OwnerUsername = earning.OwnerUsername,
            Label = earning.Label,
            AnnualGrossSalary = earning.AnnualGrossSalary,
            DeductionMode = earning.DeductionMode,
            DeductionValue = earning.DeductionValue,
            CurrencyCode = earning.CurrencyCode,
            CreatedAt = earning.CreatedAt,
            UpdatedAt = earning.UpdatedAt
        };
    }

    private static EarningsSummaryResponseDto MapToResponseDto(EarningsSummaryResponse summary)
    {
        return new EarningsSummaryResponseDto
        {
            Entries = summary.Entries.Select(item => new EarningSummaryEntryDto
            {
                Id = item.Id,
                Label = item.Label,
                AnnualGrossSalary = item.AnnualGrossSalary,
                DeductionMode = item.DeductionMode,
                DeductionValue = item.DeductionValue,
                CurrencyCode = item.CurrencyCode,
                AnnualDeduction = item.AnnualDeduction,
                AnnualNetSalary = item.AnnualNetSalary,
                GrossBreakdown = MapToResponseDto(item.GrossBreakdown),
                NetBreakdown = MapToResponseDto(item.NetBreakdown)
            }).ToList(),
            TotalGross = MapToResponseDto(summary.TotalGross),
            TotalNet = MapToResponseDto(summary.TotalNet),
            TotalAnnualDeductions = summary.TotalAnnualDeductions
        };
    }

    private static EarningsPeriodBreakdownDto MapToResponseDto(EarningsPeriodBreakdown breakdown)
    {
        return new EarningsPeriodBreakdownDto
        {
            Annual = breakdown.Annual,
            Monthly = breakdown.Monthly,
            BiWeekly = breakdown.BiWeekly,
            Weekly = breakdown.Weekly,
            Daily = breakdown.Daily,
            Hourly = breakdown.Hourly
        };
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }
}