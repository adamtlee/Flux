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
        ValidateRateFields(account);

        account.OwnerUserId = userId;
        account.Owner = username;
        account.AccountName = string.IsNullOrWhiteSpace(account.AccountName) ? username : account.AccountName.Trim();
        NormalizeRateFields(account);
        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task<bool> UpdateAccountAsync(Guid id, BankAccount account, Guid userId, bool isAdministrator)
    {
        if (id != account.Id) return false;

        ValidateRateFields(account);

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
        existing.CreditCardAprPercent = account.CreditCardAprPercent;
        existing.SavingsApyPercent = account.SavingsApyPercent;
        NormalizeRateFields(existing);
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

    private static void ValidateRateFields(BankAccount account)
    {
        if (account.CreditCardAprPercent is < 0m or > 100m)
        {
            throw new ArgumentException("CreditCardAprPercent must be between 0 and 100.");
        }

        if (account.SavingsApyPercent is < 0m or > 100m)
        {
            throw new ArgumentException("SavingsApyPercent must be between 0 and 100.");
        }
    }

    private static void NormalizeRateFields(BankAccount account)
    {
        if (account.Type != AccountType.CreditCard)
        {
            account.CreditCardAprPercent = null;
        }

        if (account.Type != AccountType.Savings)
        {
            account.SavingsApyPercent = null;
        }
    }
}