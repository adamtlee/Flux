using Flux.Data.Models;
using Flux.Services.Models;

namespace Flux.Services;

public interface IEarningsService
{
    Task<IReadOnlyList<Earning>> GetEarningsAsync(Guid userId, bool isAdministrator);
    Task<Earning?> GetEarningByIdAsync(int id, Guid userId, bool isAdministrator);
    Task<Earning> CreateEarningAsync(Guid userId, string username, EarningUpsertModel model);
    Task<Earning?> UpdateEarningAsync(int id, Guid userId, bool isAdministrator, EarningUpsertModel model);
    Task<bool> DeleteEarningAsync(int id, Guid userId, bool isAdministrator);
    Task<EarningsSummaryResponse> GetSummaryAsync(Guid userId, bool isAdministrator);
}