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
public sealed class ReceiptsController(
    IReceiptService receiptService,
    ILogger<ReceiptsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ReceiptResponseDto>>> GetReceipts()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var receipts = await receiptService.GetReceiptsAsync(userId, isAdministrator);

        return Ok(receipts.Select(MapToResponseDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReceiptResponseDto>> GetReceiptById(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Receipt ID must be a positive integer." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var receipt = await receiptService.GetReceiptByIdAsync(id, userId, isAdministrator);
        if (receipt is null)
        {
            return NotFound(new { message = $"Receipt with ID {id} not found." });
        }

        return Ok(MapToResponseDto(receipt));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ReceiptResponseDto>> CreateReceipt([FromBody] ReceiptCreateRequestDto request)
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

            var created = await receiptService.CreateReceiptAsync(userId, username, MapToModel(request));
            return CreatedAtAction(nameof(GetReceiptById), new { id = created.Id }, MapToResponseDto(created));
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized receipt creation attempt.");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return CreateValidationErrorResponse(ex, "receipt creation");
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateReceipt(int id, [FromBody] ReceiptUpdateRequestDto request)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Receipt ID must be a positive integer." });
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
            var updated = await receiptService.UpdateReceiptAsync(id, userId, isAdministrator, MapToModel(request));
            if (updated is null)
            {
                return NotFound(new { message = $"Receipt with ID {id} not found." });
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized receipt update attempt.");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return CreateValidationErrorResponse(ex, "receipt update");
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteReceipt(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Receipt ID must be a positive integer." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var deleted = await receiptService.DeleteReceiptAsync(id, userId, isAdministrator);
        if (!deleted)
        {
            return NotFound(new { message = $"Receipt with ID {id} not found." });
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

    private static ReceiptUpsertModel MapToModel(ReceiptCreateRequestDto request)
    {
        return new ReceiptUpsertModel(
            request.AccountId,
            request.MerchantName,
            request.PurchasedAtUtc,
            request.TotalAmount,
            request.CurrencyCode,
            request.Notes,
            request.Items.Select(item => new ReceiptItemUpsertModel(item.ProductName, item.Quantity, item.UnitPrice)).ToList());
    }

    private static ReceiptUpsertModel MapToModel(ReceiptUpdateRequestDto request)
    {
        return new ReceiptUpsertModel(
            request.AccountId,
            request.MerchantName,
            request.PurchasedAtUtc,
            request.TotalAmount,
            request.CurrencyCode,
            request.Notes,
            request.Items.Select(item => new ReceiptItemUpsertModel(item.ProductName, item.Quantity, item.UnitPrice)).ToList());
    }

    private static ReceiptResponseDto MapToResponseDto(Receipt receipt)
    {
        return new ReceiptResponseDto
        {
            Id = receipt.Id,
            OwnerUserId = receipt.OwnerUserId,
            OwnerUsername = receipt.OwnerUsername,
            AccountId = receipt.AccountId,
            MerchantName = receipt.MerchantName,
            PurchasedAtUtc = receipt.PurchasedAtUtc,
            TotalAmount = receipt.TotalAmount,
            CurrencyCode = receipt.CurrencyCode,
            Notes = receipt.Notes,
            Items = receipt.Items
                .Select(item => new ReceiptItemResponseDto
                {
                    Id = item.Id,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                })
                .ToList(),
            CreatedAt = receipt.CreatedAt,
            UpdatedAt = receipt.UpdatedAt
        };
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }
}
