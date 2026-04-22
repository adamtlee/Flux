using Flux.Data.Models;
using Flux.Services.Models;

namespace Flux.Services;

public interface ISubscriptionService
{
    Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(Guid userId, bool isAdministrator, SubscriptionQueryModel query);
    Task<Subscription?> GetSubscriptionByIdAsync(int id, Guid userId, bool isAdministrator);
    Task<Subscription> CreateSubscriptionAsync(Guid userId, string username, SubscriptionUpsertModel model);
    Task<Subscription?> UpdateSubscriptionAsync(int id, Guid userId, bool isAdministrator, SubscriptionUpsertModel model);
    Task<bool> CancelSubscriptionAsync(int id, Guid userId, bool isAdministrator);
    Task<IReadOnlyList<Subscription>> GetUpcomingRemindersAsync(Guid userId, bool isAdministrator, int withinDays);
    Task<SubscriptionSpendAnalyticsResponse> GetMonthlySpendAnalyticsAsync(Guid userId, bool isAdministrator, int months);
}
