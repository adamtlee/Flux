using Flux.Data.Models;
using Flux.Services.Models;
namespace Flux.Services;

public interface IBankAccountService
{
    Task<IEnumerable<BankAccount>> GetAllAccountsAsync(Guid userId, bool isAdministrator);
    Task<BankAccount?> GetAccountByIdAsync(int id, Guid userId, bool isAdministrator);
    Task<BankAccount> CreateAccountAsync(BankAccount account, Guid userId, string username);
    Task<bool> UpdateAccountAsync(int id, BankAccount account, Guid userId, bool isAdministrator);
    Task<bool> DeleteAccountAsync(int id, Guid userId, bool isAdministrator);
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