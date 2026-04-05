namespace Flux.Data.Models;

public class BankAccount
{
    public int Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string AccountName { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public AccountType Type { get; set; }

    public decimal? CreditCardAprPercent { get; set; }

    public decimal? SavingsApyPercent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum AccountType
{
    Checking,
    Savings,
    CreditCard
}

// What other properties would you add here?