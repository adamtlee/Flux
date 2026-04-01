using Flux.Data.Models;
using Flux.Services.Models;
namespace Flux.Services;

public interface IBankAccountService
{
    Task<IEnumerable<BankAccount>> GetAllAccountsAsync(Guid userId, bool isAdministrator);
    Task<BankAccount?> GetAccountByIdAsync(Guid id, Guid userId, bool isAdministrator);
    Task<BankAccount> CreateAccountAsync(BankAccount account, Guid userId, string username);
    Task<bool> UpdateAccountAsync(Guid id, BankAccount account, Guid userId, bool isAdministrator);
    Task<bool> DeleteAccountAsync(Guid id, Guid userId, bool isAdministrator);
    Task<BankAccountImportResult> ImportAccountsAsync(
        Stream fileStream,
        string fileName,
        Guid currentUserId,
        string currentUsername,
        bool isAdministrator,
        Guid? targetUserId);
    Task<byte[]> ExportAccountsAsync(Guid currentUserId, bool isAdministrator, Guid? targetUserId, BankAccountFileFormat format);
    byte[] GetImportTemplate(BankAccountFileFormat format);
}