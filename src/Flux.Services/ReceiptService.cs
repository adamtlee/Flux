using Flux.Data;
using Flux.Data.Models;
using Flux.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace Flux.Services;

public sealed class ReceiptService(BankDbContext context) : IReceiptService
{
    public async Task<IReadOnlyList<Receipt>> GetReceiptsAsync(Guid userId, bool isAdministrator)
    {
        var query = context.Receipts
            .AsNoTracking()
            .Include(receipt => receipt.Items)
            .OrderByDescending(receipt => receipt.PurchasedAtUtc)
            .ThenByDescending(receipt => receipt.CreatedAt)
            .AsQueryable();

        if (!isAdministrator)
        {
            query = query.Where(receipt => receipt.OwnerUserId == userId);
        }

        return await query.ToListAsync();
    }

    public async Task<Receipt?> GetReceiptByIdAsync(int id, Guid userId, bool isAdministrator)
    {
        var receipt = await context.Receipts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (receipt is null)
        {
            return null;
        }

        if (!isAdministrator && receipt.OwnerUserId != userId)
        {
            return null;
        }

        return receipt;
    }

    public async Task<Receipt> CreateReceiptAsync(Guid userId, string username, ReceiptUpsertModel model)
    {
        await ValidateModelAsync(model, userId, isAdministrator: false);

        var utcNow = DateTime.UtcNow;
        var receipt = new Receipt
        {
            OwnerUserId = userId,
            OwnerUsername = username,
            AccountId = model.AccountId,
            MerchantName = model.MerchantName.Trim(),
            PurchasedAtUtc = DateTime.SpecifyKind(model.PurchasedAtUtc, DateTimeKind.Utc),
            TotalAmount = model.TotalAmount,
            CurrencyCode = NormalizeCurrencyCode(model.CurrencyCode),
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Items = BuildItems(model.Items)
        };

        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();
        return receipt;
    }

    public async Task<Receipt?> UpdateReceiptAsync(int id, Guid userId, bool isAdministrator, ReceiptUpsertModel model)
    {
        var receipt = await context.Receipts
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (receipt is null)
        {
            return null;
        }

        if (!isAdministrator && receipt.OwnerUserId != userId)
        {
            return null;
        }

        await ValidateModelAsync(model, receipt.OwnerUserId, isAdministrator);

        receipt.AccountId = model.AccountId;
        receipt.MerchantName = model.MerchantName.Trim();
        receipt.PurchasedAtUtc = DateTime.SpecifyKind(model.PurchasedAtUtc, DateTimeKind.Utc);
        receipt.TotalAmount = model.TotalAmount;
        receipt.CurrencyCode = NormalizeCurrencyCode(model.CurrencyCode);
        receipt.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        receipt.UpdatedAt = DateTime.UtcNow;

        context.ReceiptItems.RemoveRange(receipt.Items);
        receipt.Items = BuildItems(model.Items);

        await context.SaveChangesAsync();
        return receipt;
    }

    public async Task<bool> DeleteReceiptAsync(int id, Guid userId, bool isAdministrator)
    {
        var receipt = await context.Receipts.FirstOrDefaultAsync(item => item.Id == id);
        if (receipt is null)
        {
            return false;
        }

        if (!isAdministrator && receipt.OwnerUserId != userId)
        {
            return false;
        }

        context.Receipts.Remove(receipt);
        await context.SaveChangesAsync();
        return true;
    }

    private async Task ValidateModelAsync(ReceiptUpsertModel model, Guid ownerUserId, bool isAdministrator)
    {
        if (string.IsNullOrWhiteSpace(model.MerchantName))
        {
            throw new ArgumentException("MerchantName is required.");
        }

        if (model.TotalAmount < 0m)
        {
            throw new ArgumentException("TotalAmount must be greater than or equal to zero.");
        }

        if (model.PurchasedAtUtc == default)
        {
            throw new ArgumentException("PurchasedAtUtc is required.");
        }

        if (model.Items.Count == 0)
        {
            throw new ArgumentException("At least one receipt item is required.");
        }

        foreach (var item in model.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                throw new ArgumentException("Each receipt item requires ProductName.");
            }

            if (item.Quantity <= 0m)
            {
                throw new ArgumentException("Each receipt item requires Quantity greater than zero.");
            }

            if (item.UnitPrice < 0m)
            {
                throw new ArgumentException("Each receipt item requires UnitPrice greater than or equal to zero.");
            }
        }

        var computedTotal = model.Items.Sum(item => item.Quantity * item.UnitPrice);
        if (Math.Abs(computedTotal - model.TotalAmount) > 0.01m)
        {
            throw new ArgumentException("TotalAmount must equal the sum of all receipt line totals.");
        }

        if (!string.IsNullOrWhiteSpace(model.CurrencyCode) && model.CurrencyCode.Trim().Length != 3)
        {
            throw new ArgumentException("CurrencyCode must be a 3-letter ISO currency code.");
        }

        if (model.AccountId.HasValue)
        {
            var account = await context.Accounts.FirstOrDefaultAsync(item => item.Id == model.AccountId.Value);
            if (account is null)
            {
                throw new ArgumentException($"Account with ID {model.AccountId.Value} was not found.");
            }

            if (!isAdministrator && account.OwnerUserId != ownerUserId)
            {
                throw new UnauthorizedAccessException("Selected account is not owned by current user.");
            }
        }
    }

    private static List<ReceiptItem> BuildItems(IReadOnlyList<ReceiptItemUpsertModel> items)
    {
        return items
            .Select(item => new ReceiptItem
            {
                ProductName = item.ProductName.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = item.Quantity * item.UnitPrice
            })
            .ToList();
    }

    private static string NormalizeCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return "USD";
        }

        return currencyCode.Trim().ToUpperInvariant();
    }
}
