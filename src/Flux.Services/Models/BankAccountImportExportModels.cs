namespace Flux.Services.Models;

public enum BankAccountFileFormat
{
    Csv,
    Xlsx
}

public sealed record BankAccountImportResult(
    int RowsProcessed,
    int CreatedCount,
    int UpdatedCount,
    string Message
);