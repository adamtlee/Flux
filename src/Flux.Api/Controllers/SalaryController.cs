using Flux.Api.Contracts;
using Flux.Services;
using Flux.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flux.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.FreeMember)]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SalaryController(
    ISalaryService salaryService,
    ILogger<SalaryController> logger) : ControllerBase
{
    [HttpPost("calculate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<SalaryCalculationResponseDto> Calculate([FromBody] SalaryCalculateRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var model = new SalaryCalculationRequest(
                request.GrossAnnualSalary,
                request.CurrencyCode,
                request.Deductions
                    .Select(d => new SalaryDeductionInput(d.Name, d.Type, d.Value))
                    .ToList());

            var result = salaryService.Calculate(model);

            return Ok(new SalaryCalculationResponseDto
            {
                GrossAnnual = result.GrossAnnual,
                CurrencyCode = result.CurrencyCode,
                DeductionBreakdown = result.DeductionBreakdown
                    .Select(d => new SalaryDeductionResultDto { Name = d.Name, AnnualAmount = d.AnnualAmount })
                    .ToList(),
                TotalDeductionsAnnual = result.TotalDeductionsAnnual,
                NetAnnual = result.NetAnnual,
                NetMonthly = result.NetMonthly,
                NetBiweekly = result.NetBiweekly,
                NetWeekly = result.NetWeekly,
                NetDaily = result.NetDaily,
                NetHourly = result.NetHourly
            });
        }
        catch (ArgumentException ex)
        {
            var correlationId = HttpContext.TraceIdentifier;
            logger.LogWarning(
                ex,
                "Validation error during salary calculation. CorrelationId: {CorrelationId}",
                correlationId);

            return BadRequest(new { message = ex.Message, correlationId });
        }
    }
}
