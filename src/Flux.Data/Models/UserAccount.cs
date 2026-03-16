namespace Flux.Data.Models;

public class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public int PasswordIterations { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
