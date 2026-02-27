using Flux.Data.Models;
namespace Flux.Services;

public interface IBankAccountService
{
  Task<IEnumerable<BankAccount>> GetAllAccountsAsync();
    Task<BankAccount?> GetAccountByIdAsync(Guid id);
    Task<BankAccount> CreateAccountAsync(BankAccount account);
    Task<bool> UpdateAccountAsync(Guid id, BankAccount account);
    Task<bool> DeleteAccountAsync(Guid id);
}