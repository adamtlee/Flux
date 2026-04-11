using System.ComponentModel.DataAnnotations;
using Flux.Data.Models;

namespace Flux.Api.Contracts;

public sealed class BankAccountCreateRequestDto
{
    [Required]
    [StringLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [Range(typeof(decimal), "-79228162514264337593543950335", "79228162514264337593543950335")]
    public decimal Balance { get; set; }

    [Required]
    public AccountType Type { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? CreditCardAprPercent { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? SavingsApyPercent { get; set; }
}

public sealed class BankAccountUpdateRequestDto
{
    [Required]
    [StringLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [Range(typeof(decimal), "-79228162514264337593543950335", "79228162514264337593543950335")]
    public decimal Balance { get; set; }

    [Required]
    public AccountType Type { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? CreditCardAprPercent { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal? SavingsApyPercent { get; set; }
}

public sealed class BankAccountResponseDto
{
    public int Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string AccountName { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public AccountType Type { get; set; }

    public decimal? CreditCardAprPercent { get; set; }

    public decimal? SavingsApyPercent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
