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

    public async Task<byte[]> ExportReceiptsAsync(Guid currentUserId, bool isAdministrator, Guid? targetUserId, ReceiptFileFormat format)
    {
        var effectiveUserId = await ResolveEffectiveOwnerIdAsync(currentUserId, isAdministrator, targetUserId);

        var receipts = await context.Receipts
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.OwnerUserId == effectiveUserId)
            .OrderByDescending(r => r.PurchasedAtUtc)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        return format switch
        {
            ReceiptFileFormat.Csv => BuildExportCsv(receipts),
            ReceiptFileFormat.Xlsx => BuildExportXlsx(receipts),
            _ => throw new ArgumentException("Unsupported export format.")
        };
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

    private static byte[] BuildExportCsv(IEnumerable<Receipt> receipts)
    {
        using var stringWriter = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        using var csv = new CsvHelper.CsvWriter(stringWriter, System.Globalization.CultureInfo.InvariantCulture);

        csv.WriteField("Id");
        csv.WriteField("MerchantName");
        csv.WriteField("PurchasedAtUtc");
        csv.WriteField("TotalAmount");
        csv.WriteField("CurrencyCode");
        csv.WriteField("OwnerUserId");
        csv.WriteField("OwnerUsername");
        csv.WriteField("AccountId");
        csv.WriteField("Notes");
        csv.WriteField("CreatedAt");
        csv.WriteField("UpdatedAt");
        csv.NextRecord();

        foreach (var r in receipts)
        {
            csv.WriteField(r.Id);
            csv.WriteField(r.MerchantName);
            csv.WriteField(r.PurchasedAtUtc);
            csv.WriteField(r.TotalAmount);
            csv.WriteField(r.CurrencyCode);
            csv.WriteField(r.OwnerUserId);
            csv.WriteField(r.OwnerUsername);
            csv.WriteField(r.AccountId);
            csv.WriteField(r.Notes);
            csv.WriteField(r.CreatedAt);
            csv.WriteField(r.UpdatedAt);
            csv.NextRecord();
        }

        return System.Text.Encoding.UTF8.GetBytes(stringWriter.ToString());
    }

    private static byte[] BuildExportXlsx(IEnumerable<Receipt> receipts)
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Receipts");

        var headers = new[]
        {
            "Id",
            "MerchantName",
            "PurchasedAtUtc",
            "TotalAmount",
            "CurrencyCode",
            "OwnerUserId",
            "OwnerUsername",
            "AccountId",
            "Notes",
            "CreatedAt",
            "UpdatedAt"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var r in receipts)
        {
            worksheet.Cell(row, 1).Value = r.Id.ToString();
            worksheet.Cell(row, 2).Value = r.MerchantName;
            worksheet.Cell(row, 3).Value = r.PurchasedAtUtc;
            worksheet.Cell(row, 4).Value = r.TotalAmount;
            worksheet.Cell(row, 5).Value = r.CurrencyCode;
            worksheet.Cell(row, 6).Value = r.OwnerUserId.ToString();
            worksheet.Cell(row, 7).Value = r.OwnerUsername;
            worksheet.Cell(row, 8).Value = r.AccountId?.ToString();
            worksheet.Cell(row, 9).Value = r.Notes;
            worksheet.Cell(row, 10).Value = r.CreatedAt;
            worksheet.Cell(row, 11).Value = r.UpdatedAt;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<Guid> ResolveEffectiveOwnerIdAsync(Guid currentUserId, bool isAdministrator, Guid? targetUserId)
    {
        if (!targetUserId.HasValue || targetUserId.Value == Guid.Empty)
        {
            return currentUserId;
        }

        if (!isAdministrator)
        {
            throw new UnauthorizedAccessException("Only administrators can target another user.");
        }

        var user = await context.UserAccounts.FirstOrDefaultAsync(u => u.Id == targetUserId.Value);
        if (user is null)
        {
            throw new ArgumentException($"Target user with ID {targetUserId.Value} was not found.");
        }

        return user.Id;
    }
}
