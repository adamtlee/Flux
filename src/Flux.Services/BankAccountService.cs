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

    public async Task<IEnumerable<BankAccount>> GetAllAccountsAsync(Guid userId, bool isAdministrator)
    {
        if (isAdministrator)
        {
            return await _context.Accounts.ToListAsync();
        }

        return await _context.Accounts
            .Where(account => account.OwnerUserId == userId)
            .ToListAsync();
    }

    public async Task<BankAccount?> GetAccountByIdAsync(Guid id, Guid userId, bool isAdministrator)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account is null)
        {
            return null;
        }

        if (!isAdministrator && account.OwnerUserId != userId)
        {
            return null;
        }

        return account;
    }

    public async Task<BankAccount> CreateAccountAsync(BankAccount account, Guid userId, string username)
    {
        account.OwnerUserId = userId;
        account.Owner = username;
        account.AccountName = string.IsNullOrWhiteSpace(account.AccountName) ? username : account.AccountName.Trim();
        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task<bool> UpdateAccountAsync(Guid id, BankAccount account, Guid userId, bool isAdministrator)
    {
        if (id != account.Id) return false;

        var existing = await _context.Accounts.FindAsync(id);
        if (existing is null)
        {
            return false;
        }

        if (!isAdministrator && existing.OwnerUserId != userId)
        {
            return false;
        }

        existing.Balance = account.Balance;
        existing.Type = account.Type;
        existing.AccountName = string.IsNullOrWhiteSpace(account.AccountName) ? existing.AccountName : account.AccountName.Trim();
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAccountAsync(Guid id, Guid userId, bool isAdministrator)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null) return false;

        if (!isAdministrator && account.OwnerUserId != userId)
        {
            return false;
        }
        
        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();
        return true;
    }
}