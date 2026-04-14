using System.ComponentModel.DataAnnotations;

namespace Flux.Api.Contracts;

public sealed class ReceiptItemRequestDto
{
    [Required]
    [StringLength(150)]
    public string ProductName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; set; }
}

public sealed class ReceiptCreateRequestDto
{
    public int? AccountId { get; set; }

    [Required]
    [StringLength(150)]
    public string MerchantName { get; set; } = string.Empty;

    [Required]
    public DateTime PurchasedAtUtc { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal TotalAmount { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [StringLength(500)]
    public string? Notes { get; set; }

    [MinLength(1)]
    public List<ReceiptItemRequestDto> Items { get; set; } = [];
}

public sealed class ReceiptUpdateRequestDto
{
    public int? AccountId { get; set; }

    [Required]
    [StringLength(150)]
    public string MerchantName { get; set; } = string.Empty;

    [Required]
    public DateTime PurchasedAtUtc { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal TotalAmount { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [StringLength(500)]
    public string? Notes { get; set; }

    [MinLength(1)]
    public List<ReceiptItemRequestDto> Items { get; set; } = [];
}

public sealed class ReceiptItemResponseDto
{
    public int Id { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

public sealed class ReceiptResponseDto
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

    public IReadOnlyList<ReceiptItemResponseDto> Items { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
