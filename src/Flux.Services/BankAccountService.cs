using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Flux.Data.Models;
using Flux.Data;
using Flux.Services.Models;

namespace Flux.Services;

public class BankAccountService : IBankAccountService
{
    private static readonly string[] ImportHeaders =
    [
        "Id",
        "AccountName",
        "Balance",
        "Type",
        "CreditCardAprPercent",
        "SavingsApyPercent"
    ];

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

    public async Task<BankAccount?> GetAccountByIdAsync(int id, Guid userId, bool isAdministrator)
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

    public async Task<bool> UpdateAccountAsync(int id, BankAccount account, Guid userId, bool isAdministrator)
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

    public async Task<bool> DeleteAccountAsync(int id, Guid userId, bool isAdministrator)
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

    public async Task<BankAccountImportResult> ImportAccountsAsync(
        Stream fileStream,
        string fileName,
        Guid currentUserId,
        string currentUsername,
        bool isAdministrator,
        Guid? targetUserId)
    {
        var format = ResolveFormatFromFileName(fileName);
        var (effectiveUserId, effectiveUsername) = await ResolveEffectiveOwnerAsync(
            currentUserId,
            currentUsername,
            isAdministrator,
            targetUserId);

        List<ParsedImportRow> rows = format switch
        {
            BankAccountFileFormat.Csv => ParseCsvRows(fileStream),
            BankAccountFileFormat.Xlsx => ParseXlsxRows(fileStream),
            _ => throw new ArgumentException("Unsupported import file format.")
        };

        if (rows.Count == 0)
        {
            throw new ArgumentException("The import file does not contain any rows.");
        }

        var duplicateId = rows
            .Where(row => row.Id.HasValue)
            .GroupBy(row => row.Id!.Value)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateId is not null)
        {
            throw new ArgumentException($"Duplicate account ID detected in import file: {duplicateId.Key}.");
        }

        var idsInFile = rows.Where(row => row.Id.HasValue).Select(row => row.Id!.Value).ToHashSet();
        var existingById = await _context.Accounts
            .Where(account => idsInFile.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id);

        var createdCount = 0;
        var updatedCount = 0;
        var createdAccounts = new List<BankAccount>();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        foreach (var row in rows)
        {
            if (row.Id.HasValue && existingById.TryGetValue(row.Id.Value, out var existing))
            {
                if (!isAdministrator && existing.OwnerUserId != effectiveUserId)
                {
                    throw new UnauthorizedAccessException($"Row {row.RowNumber}: account ID {existing.Id} is not owned by the current user.");
                }

                if (isAdministrator && existing.OwnerUserId != effectiveUserId)
                {
                    throw new ArgumentException($"Row {row.RowNumber}: account ID {existing.Id} belongs to a different user than the selected target user.");
                }

                existing.AccountName = string.IsNullOrWhiteSpace(row.AccountName)
                    ? existing.AccountName
                    : row.AccountName.Trim();
                existing.Balance = row.Balance;
                existing.Type = row.Type;
                existing.CreditCardAprPercent = row.CreditCardAprPercent;
                existing.SavingsApyPercent = row.SavingsApyPercent;
                existing.OwnerUserId = effectiveUserId;
                existing.Owner = effectiveUsername;
                ValidateRateFields(existing);
                NormalizeRateFields(existing);
                existing.UpdatedAt = DateTime.UtcNow;
                updatedCount++;
                continue;
            }

            var newAccount = new BankAccount
            {
                OwnerUserId = effectiveUserId,
                Owner = effectiveUsername,
                AccountName = string.IsNullOrWhiteSpace(row.AccountName) ? effectiveUsername : row.AccountName.Trim(),
                Balance = row.Balance,
                Type = row.Type,
                CreditCardAprPercent = row.CreditCardAprPercent,
                SavingsApyPercent = row.SavingsApyPercent,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (row.Id.HasValue)
            {
                newAccount.Id = row.Id.Value;
            }

            ValidateRateFields(newAccount);
            NormalizeRateFields(newAccount);

            createdAccounts.Add(newAccount);
            createdCount++;
        }

        if (createdAccounts.Count > 0)
        {
            _context.Accounts.AddRange(createdAccounts);
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new BankAccountImportResult(
            RowsProcessed: rows.Count,
            CreatedCount: createdCount,
            UpdatedCount: updatedCount,
            Message: "Import completed successfully.");
    }

    public async Task<byte[]> ExportAccountsAsync(Guid currentUserId, bool isAdministrator, Guid? targetUserId, BankAccountFileFormat format)
    {
        var (effectiveUserId, _) = await ResolveEffectiveOwnerAsync(
            currentUserId,
            string.Empty,
            isAdministrator,
            targetUserId);

        var accounts = await _context.Accounts
            .Where(account => account.OwnerUserId == effectiveUserId)
            .OrderBy(account => account.AccountName)
            .ThenBy(account => account.CreatedAt)
            .ToListAsync();

        return format switch
        {
            BankAccountFileFormat.Csv => BuildExportCsv(accounts),
            BankAccountFileFormat.Xlsx => BuildExportXlsx(accounts),
            _ => throw new ArgumentException("Unsupported export format.")
        };
    }

    public byte[] GetImportTemplate(BankAccountFileFormat format)
    {
        return format switch
        {
            BankAccountFileFormat.Csv => BuildTemplateCsv(),
            BankAccountFileFormat.Xlsx => BuildTemplateXlsx(),
            _ => throw new ArgumentException("Unsupported template format.")
        };
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

    private async Task<(Guid EffectiveUserId, string EffectiveUsername)> ResolveEffectiveOwnerAsync(
        Guid currentUserId,
        string currentUsername,
        bool isAdministrator,
        Guid? targetUserId)
    {
        if (!targetUserId.HasValue || targetUserId.Value == Guid.Empty)
        {
            var fallbackUsername = string.IsNullOrWhiteSpace(currentUsername)
                ? await ResolveUsernameByIdAsync(currentUserId)
                : currentUsername;
            return (currentUserId, fallbackUsername);
        }

        if (!isAdministrator)
        {
            throw new UnauthorizedAccessException("Only administrators can target another user.");
        }

        var user = await _context.UserAccounts.FirstOrDefaultAsync(account => account.Id == targetUserId.Value);
        if (user is null)
        {
            throw new ArgumentException($"Target user with ID {targetUserId.Value} was not found.");
        }

        return (user.Id, user.Username);
    }

    private async Task<string> ResolveUsernameByIdAsync(Guid userId)
    {
        var user = await _context.UserAccounts.FirstOrDefaultAsync(account => account.Id == userId);
        if (user is null)
        {
            throw new ArgumentException($"User with ID {userId} was not found.");
        }

        return user.Username;
    }

    private static BankAccountFileFormat ResolveFormatFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.Trim().ToLowerInvariant();
        return extension switch
        {
            ".csv" => BankAccountFileFormat.Csv,
            ".xlsx" => BankAccountFileFormat.Xlsx,
            _ => throw new ArgumentException("Only .csv and .xlsx files are supported for import.")
        };
    }

    private static List<ParsedImportRow> ParseCsvRows(Stream fileStream)
    {
        fileStream.Position = 0;

        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        });

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new ArgumentException("The CSV file is empty or missing a header row.");
        }

        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var indexByHeader = BuildHeaderIndex(headers);
        EnsureRequiredHeaders(indexByHeader.Keys);

        var rows = new List<ParsedImportRow>();
        while (csv.Read())
        {
            var rowNumber = csv.Parser.Row;
            var rawRow = ReadRawRow(indexByHeader, header => csv.GetField(indexByHeader[header]));
            if (IsEmptyRow(rawRow))
            {
                continue;
            }

            rows.Add(ParseRow(rawRow, rowNumber));
        }

        return rows;
    }

    private static List<ParsedImportRow> ParseXlsxRows(Stream fileStream)
    {
        fileStream.Position = 0;
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            throw new ArgumentException("The XLSX workbook does not contain any worksheet.");
        }

        var headerRow = worksheet.Row(1);
        var lastHeaderColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        if (lastHeaderColumn == 0)
        {
            throw new ArgumentException("The XLSX file is missing a header row.");
        }

        var headers = Enumerable.Range(1, lastHeaderColumn)
            .Select(column => worksheet.Cell(1, column).GetString())
            .ToArray();
        var indexByHeader = BuildHeaderIndex(headers);
        EnsureRequiredHeaders(indexByHeader.Keys);

        var rows = new List<ParsedImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var rawRow = ReadRawRow(indexByHeader, header => worksheet.Cell(rowNumber, indexByHeader[header] + 1).GetString());
            if (IsEmptyRow(rawRow))
            {
                continue;
            }

            rows.Add(ParseRow(rawRow, rowNumber));
        }

        return rows;
    }

    private static Dictionary<string, int> BuildHeaderIndex(IEnumerable<string> headers)
    {
        var indexByHeader = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var header in headers)
        {
            var normalized = NormalizeHeader(header);
            if (!string.IsNullOrWhiteSpace(normalized) && !indexByHeader.ContainsKey(normalized))
            {
                indexByHeader[normalized] = index;
            }

            index++;
        }

        return indexByHeader;
    }

    private static void EnsureRequiredHeaders(IEnumerable<string> headers)
    {
        var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        var missingHeaders = ImportHeaders.Where(required => !headerSet.Contains(required)).ToList();
        if (missingHeaders.Count > 0)
        {
            throw new ArgumentException($"Import file is missing required columns: {string.Join(", ", missingHeaders)}.");
        }
    }

    private static Dictionary<string, string?> ReadRawRow(
        Dictionary<string, int> headerIndex,
        Func<string, string?> fieldReader)
    {
        var rawRow = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in ImportHeaders)
        {
            rawRow[header] = fieldReader(header);
        }

        return rawRow;
    }

    private static bool IsEmptyRow(Dictionary<string, string?> row)
    {
        return row.Values.All(value => string.IsNullOrWhiteSpace(value));
    }

    private static ParsedImportRow ParseRow(Dictionary<string, string?> rawRow, int rowNumber)
    {
        var idValue = (rawRow["Id"] ?? string.Empty).Trim();
        int? id = null;
        if (!string.IsNullOrWhiteSpace(idValue))
        {
            if (!int.TryParse(idValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId) || parsedId <= 0)
            {
                throw new ArgumentException($"Row {rowNumber}: Id must be a positive integer.");
            }

            id = parsedId;
        }

        var accountName = (rawRow["AccountName"] ?? string.Empty).Trim();

        var balanceValue = (rawRow["Balance"] ?? string.Empty).Trim();
        if (!decimal.TryParse(balanceValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var balance))
        {
            throw new ArgumentException($"Row {rowNumber}: Balance must be a valid decimal value.");
        }

        var typeValue = (rawRow["Type"] ?? string.Empty).Trim();
        if (!TryParseAccountType(typeValue, out var accountType))
        {
            throw new ArgumentException($"Row {rowNumber}: Type must be one of Checking, Savings, CreditCard, 0, 1, or 2.");
        }

        var creditCardAprPercent = ParseNullableDecimal(rawRow["CreditCardAprPercent"], rowNumber, "CreditCardAprPercent");
        var savingsApyPercent = ParseNullableDecimal(rawRow["SavingsApyPercent"], rowNumber, "SavingsApyPercent");

        return new ParsedImportRow(
            RowNumber: rowNumber,
            Id: id,
            AccountName: accountName,
            Balance: balance,
            Type: accountType,
            CreditCardAprPercent: creditCardAprPercent,
            SavingsApyPercent: savingsApyPercent);
    }

    private static bool TryParseAccountType(string value, out AccountType accountType)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericType)
            && Enum.IsDefined(typeof(AccountType), numericType))
        {
            accountType = (AccountType)numericType;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out accountType)
               && Enum.IsDefined(accountType);
    }

    private static decimal? ParseNullableDecimal(string? value, int rowNumber, string columnName)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Row {rowNumber}: {columnName} must be a valid decimal value.");
        }

        return parsed;
    }

    private static string NormalizeHeader(string header)
    {
        return string.Concat((header ?? string.Empty).Where(character => character != ' '));
    }

    private static byte[] BuildExportCsv(IEnumerable<BankAccount> accounts)
    {
        using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);

        csv.WriteField("Id");
        csv.WriteField("AccountName");
        csv.WriteField("Balance");
        csv.WriteField("Type");
        csv.WriteField("CreditCardAprPercent");
        csv.WriteField("SavingsApyPercent");
        csv.WriteField("OwnerUserId");
        csv.WriteField("Owner");
        csv.WriteField("CreatedAt");
        csv.WriteField("UpdatedAt");
        csv.NextRecord();

        foreach (var account in accounts)
        {
            csv.WriteField(account.Id);
            csv.WriteField(account.AccountName);
            csv.WriteField(account.Balance);
            csv.WriteField(account.Type.ToString());
            csv.WriteField(account.CreditCardAprPercent);
            csv.WriteField(account.SavingsApyPercent);
            csv.WriteField(account.OwnerUserId);
            csv.WriteField(account.Owner);
            csv.WriteField(account.CreatedAt);
            csv.WriteField(account.UpdatedAt);
            csv.NextRecord();
        }

        return Encoding.UTF8.GetBytes(stringWriter.ToString());
    }

    private static byte[] BuildExportXlsx(IEnumerable<BankAccount> accounts)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("BankAccounts");

        var headers = new[]
        {
            "Id",
            "AccountName",
            "Balance",
            "Type",
            "CreditCardAprPercent",
            "SavingsApyPercent",
            "OwnerUserId",
            "Owner",
            "CreatedAt",
            "UpdatedAt"
        };

        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
        }

        var rowIndex = 2;
        foreach (var account in accounts)
        {
            worksheet.Cell(rowIndex, 1).Value = account.Id.ToString();
            worksheet.Cell(rowIndex, 2).Value = account.AccountName;
            worksheet.Cell(rowIndex, 3).Value = account.Balance;
            worksheet.Cell(rowIndex, 4).Value = account.Type.ToString();
            worksheet.Cell(rowIndex, 5).Value = account.CreditCardAprPercent;
            worksheet.Cell(rowIndex, 6).Value = account.SavingsApyPercent;
            worksheet.Cell(rowIndex, 7).Value = account.OwnerUserId.ToString();
            worksheet.Cell(rowIndex, 8).Value = account.Owner;
            worksheet.Cell(rowIndex, 9).Value = account.CreatedAt;
            worksheet.Cell(rowIndex, 10).Value = account.UpdatedAt;
            rowIndex++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildTemplateCsv()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var header in ImportHeaders)
        {
            csv.WriteField(header);
        }

        csv.NextRecord();
        return Encoding.UTF8.GetBytes(writer.ToString());
    }

    private static byte[] BuildTemplateXlsx()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Template");

        for (var column = 0; column < ImportHeaders.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = ImportHeaders[column];
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record ParsedImportRow(
        int RowNumber,
        int? Id,
        string AccountName,
        decimal Balance,
        AccountType Type,
        decimal? CreditCardAprPercent,
        decimal? SavingsApyPercent);
}