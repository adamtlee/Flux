namespace Flux.Services.Models;

public sealed record ReceiptItemUpsertModel(
    string ProductName,
    decimal Quantity,
    decimal UnitPrice
);

public sealed record ReceiptUpsertModel(
    int? AccountId,
    string MerchantName,
    DateTime PurchasedAtUtc,
    decimal TotalAmount,
    string CurrencyCode,
    string? Notes,
    IReadOnlyList<ReceiptItemUpsertModel> Items
);
