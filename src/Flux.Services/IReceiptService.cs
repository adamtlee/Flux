using Flux.Data.Models;
using Flux.Services.Models;

namespace Flux.Services;

public interface IReceiptService
{
    Task<IReadOnlyList<Receipt>> GetReceiptsAsync(Guid userId, bool isAdministrator);
    Task<Receipt?> GetReceiptByIdAsync(int id, Guid userId, bool isAdministrator);
    Task<Receipt> CreateReceiptAsync(Guid userId, string username, ReceiptUpsertModel model);
    Task<Receipt?> UpdateReceiptAsync(int id, Guid userId, bool isAdministrator, ReceiptUpsertModel model);
    Task<bool> DeleteReceiptAsync(int id, Guid userId, bool isAdministrator);
}
