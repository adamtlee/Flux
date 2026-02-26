namespace Flux.Data.Models;

public class BankAccount
{
    // What Id is this exactly? Is it a domain identifier? To be used as a primary key?
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Owner { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public AccountType Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}

public enum AccountType
{
    Checking,
    Savings
}

// What other properties would you add here?