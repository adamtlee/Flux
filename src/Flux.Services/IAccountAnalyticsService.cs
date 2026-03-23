using Flux.Services.Models;

namespace Flux.Services;

public interface IAccountAnalyticsService
{
    Task<PortfolioRateAnalyticsResponse> GetPortfolioAnalyticsAsync(Guid userId, bool isAdministrator);
    Task<AccountRateAnalyticsResponse?> GetAccountAnalyticsByIdAsync(Guid accountId, Guid userId, bool isAdministrator);
}