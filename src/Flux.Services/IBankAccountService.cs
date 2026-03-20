using Flux.Data.Models;
namespace Flux.Services;

public interface IBankAccountService
{
    Task<IEnumerable<BankAccount>> GetAllAccountsAsync(Guid userId, bool isAdministrator);
    Task<BankAccount?> GetAccountByIdAsync(Guid id, Guid userId, bool isAdministrator);
    Task<BankAccount> CreateAccountAsync(BankAccount account, Guid userId, string username);
    Task<bool> UpdateAccountAsync(Guid id, BankAccount account, Guid userId, bool isAdministrator);
    Task<bool> DeleteAccountAsync(Guid id, Guid userId, bool isAdministrator);
}