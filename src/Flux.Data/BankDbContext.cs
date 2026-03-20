using Microsoft.EntityFrameworkCore;
using Flux.Data.Models;

namespace Flux.Data;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<BankAccount> Accounts { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAccount>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<UserAccount>()
            .Property(user => user.Username)
            .HasMaxLength(100);

        modelBuilder.Entity<UserAccount>()
            .Property(user => user.Role)
            .HasMaxLength(50);

        modelBuilder.Entity<BankAccount>()
            .Property(account => account.Owner)
            .HasMaxLength(100);

        modelBuilder.Entity<BankAccount>()
            .Property(account => account.OwnerUserId);
    }
}