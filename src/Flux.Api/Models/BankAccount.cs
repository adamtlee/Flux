namespace Flux.Api.Models; 

public class BankAccount
{
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