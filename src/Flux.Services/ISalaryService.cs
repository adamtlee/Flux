using Flux.Services.Models;

namespace Flux.Services;

public interface ISalaryService
{
    SalaryCalculationResult Calculate(SalaryCalculationRequest request);
}
