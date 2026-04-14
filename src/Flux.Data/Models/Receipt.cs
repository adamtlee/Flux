namespace Flux.Data.Models;

public sealed class Receipt
{
    public int Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string OwnerUsername { get; set; } = string.Empty;

    public int? AccountId { get; set; }

    public string MerchantName { get; set; } = string.Empty;

    public DateTime PurchasedAtUtc { get; set; }

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public BankAccount? Account { get; set; }

    public ICollection<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();
}

public sealed class ReceiptItem
{
    public int Id { get; set; }

    public int ReceiptId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public Receipt Receipt { get; set; } = null!;
}
