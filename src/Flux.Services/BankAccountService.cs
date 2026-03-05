using Microsoft.EntityFrameworkCore;
using Flux.Data.Models;
using Flux.Data;

namespace Flux.Services;

public class BankAccountService : IBankAccountService
{
   private readonly BankDbContext _context;

    public BankAccountService(BankDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BankAccount>> GetAllAccountsAsync() => 
        await _context.Accounts.ToListAsync();

    public async Task<BankAccount?> GetAccountByIdAsync(Guid id) => 
        await _context.Accounts.FindAsync(id);

    public async Task<BankAccount> CreateAccountAsync(BankAccount account)
    {
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task<bool> UpdateAccountAsync(Guid id, BankAccount account)
    {
        if (id != account.Id) return false;
        account.UpdatedAt = DateTime.UtcNow;
        _context.Entry(account).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAccountAsync(Guid id)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null) return false;
        
        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();
        return true;
    }
}