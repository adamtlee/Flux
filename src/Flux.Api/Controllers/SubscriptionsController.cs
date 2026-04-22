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
public sealed class SubscriptionsController(
    ISubscriptionService subscriptionService,
    ILogger<SubscriptionsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<SubscriptionResponseDto>>> GetSubscriptions(
        [FromQuery] SubscriptionCategory? category,
        [FromQuery] SubscriptionStatus? status,
        [FromQuery] int? dueWithinDays,
        [FromQuery] string? tag)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var query = new SubscriptionQueryModel(category, status, dueWithinDays, tag);
        var subscriptions = await subscriptionService.GetSubscriptionsAsync(userId, isAdministrator, query);

        return Ok(subscriptions.Select(MapToResponseDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SubscriptionResponseDto>> GetSubscriptionById(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Subscription ID must be a positive integer." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var subscription = await subscriptionService.GetSubscriptionByIdAsync(id, userId, isAdministrator);
        if (subscription is null)
        {
            return NotFound(new { message = $"Subscription with ID {id} not found." });
        }

        return Ok(MapToResponseDto(subscription));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SubscriptionResponseDto>> CreateSubscription([FromBody] SubscriptionCreateRequestDto request)
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

            var created = await subscriptionService.CreateSubscriptionAsync(userId, username, MapToModel(request));
            return CreatedAtAction(nameof(GetSubscriptionById), new { id = created.Id }, MapToResponseDto(created));
        }
        catch (ArgumentException ex)
        {
            return CreateValidationErrorResponse(ex, "subscription creation");
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSubscription(int id, [FromBody] SubscriptionUpdateRequestDto request)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Subscription ID must be a positive integer." });
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
            var updated = await subscriptionService.UpdateSubscriptionAsync(id, userId, isAdministrator, MapToModel(request));
            if (updated is null)
            {
                return NotFound(new { message = $"Subscription with ID {id} not found." });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return CreateValidationErrorResponse(ex, "subscription update");
        }
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelSubscription(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Subscription ID must be a positive integer." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var cancelled = await subscriptionService.CancelSubscriptionAsync(id, userId, isAdministrator);
        if (!cancelled)
        {
            return NotFound(new { message = $"Subscription with ID {id} not found." });
        }

        return NoContent();
    }

    [HttpGet("reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<SubscriptionResponseDto>>> GetReminders([FromQuery] int withinDays = 7)
    {
        if (withinDays is < 0 or > 365)
        {
            return BadRequest(new { message = "withinDays must be between 0 and 365." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var reminders = await subscriptionService.GetUpcomingRemindersAsync(userId, isAdministrator, withinDays);
        return Ok(reminders.Select(MapToResponseDto));
    }

    [HttpGet("analytics/monthly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SubscriptionAnalyticsResponseDto>> GetMonthlyAnalytics([FromQuery] int months = 6)
    {
        if (months is < 1 or > 24)
        {
            return BadRequest(new { message = "months must be between 1 and 24." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(new { message = "User identity is missing from token." });
        }

        var isAdministrator = User.IsInRole(ApplicationRoles.Administrator);
        var analytics = await subscriptionService.GetMonthlySpendAnalyticsAsync(userId, isAdministrator, months);

        return Ok(new SubscriptionAnalyticsResponseDto
        {
            Trend = analytics.Trend
                .Select(point => new SubscriptionTrendPointDto
                {
                    Year = point.Year,
                    Month = point.Month,
                    EstimatedSpend = point.EstimatedSpend
                })
                .ToList(),
            CategoryBreakdown = analytics.CategoryBreakdown
                .Select(item => new SubscriptionCategorySpendDto
                {
                    Category = item.Category,
                    EstimatedMonthlySpend = item.EstimatedMonthlySpend,
                    SubscriptionCount = item.SubscriptionCount
                })
                .ToList(),
            CurrentMonthlyEstimatedSpend = analytics.CurrentMonthlyEstimatedSpend
        });
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

    private static SubscriptionUpsertModel MapToModel(SubscriptionCreateRequestDto request)
    {
        return new SubscriptionUpsertModel(
            request.ServiceName,
            request.ProviderName,
            request.Category,
            request.Tags,
            request.BillingCycle,
            request.Amount,
            request.CurrencyCode,
            request.StartDateUtc,
            request.NextDueDateUtc,
            request.ReminderDaysBeforeDue,
            request.AutoRenew,
            request.Status,
            request.Notes);
    }

    private static SubscriptionUpsertModel MapToModel(SubscriptionUpdateRequestDto request)
    {
        return new SubscriptionUpsertModel(
            request.ServiceName,
            request.ProviderName,
            request.Category,
            request.Tags,
            request.BillingCycle,
            request.Amount,
            request.CurrencyCode,
            request.StartDateUtc,
            request.NextDueDateUtc,
            request.ReminderDaysBeforeDue,
            request.AutoRenew,
            request.Status,
            request.Notes);
    }

    private static SubscriptionResponseDto MapToResponseDto(Subscription subscription)
    {
        return new SubscriptionResponseDto
        {
            Id = subscription.Id,
            OwnerUserId = subscription.OwnerUserId,
            OwnerUsername = subscription.OwnerUsername,
            ServiceName = subscription.ServiceName,
            ProviderName = subscription.ProviderName,
            Category = subscription.Category,
            Tags = ParseTags(subscription.TagsCsv),
            BillingCycle = subscription.BillingCycle,
            Amount = subscription.Amount,
            CurrencyCode = subscription.CurrencyCode,
            StartDateUtc = subscription.StartDateUtc,
            NextDueDateUtc = subscription.NextDueDateUtc,
            ReminderDaysBeforeDue = subscription.ReminderDaysBeforeDue,
            AutoRenew = subscription.AutoRenew,
            Status = subscription.Status,
            Notes = subscription.Notes,
            CancelledAtUtc = subscription.CancelledAtUtc,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt
        };
    }

    private static IReadOnlyList<string> ParseTags(string tagsCsv)
    {
        if (string.IsNullOrWhiteSpace(tagsCsv))
        {
            return [];
        }

        return tagsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }
}
