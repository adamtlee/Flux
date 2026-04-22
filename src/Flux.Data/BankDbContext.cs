using Microsoft.EntityFrameworkCore;
using Flux.Data.Models;

namespace Flux.Data;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<BankAccount> Accounts { get; set; }
    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<Receipt> Receipts { get; set; }
    public DbSet<ReceiptItem> ReceiptItems { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }

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
            .Property(account => account.AccountName)
            .HasMaxLength(100);

        modelBuilder.Entity<BankAccount>()
            .Property(account => account.OwnerUserId);

        modelBuilder.Entity<Receipt>()
            .Property(receipt => receipt.OwnerUsername)
            .HasMaxLength(100);

        modelBuilder.Entity<Receipt>()
            .Property(receipt => receipt.MerchantName)
            .HasMaxLength(150);

        modelBuilder.Entity<Receipt>()
            .Property(receipt => receipt.CurrencyCode)
            .HasMaxLength(3);

        modelBuilder.Entity<Receipt>()
            .Property(receipt => receipt.Notes)
            .HasMaxLength(500);

        modelBuilder.Entity<Receipt>()
            .Property(receipt => receipt.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Receipt>()
            .HasIndex(receipt => new { receipt.OwnerUserId, receipt.PurchasedAtUtc });

        modelBuilder.Entity<Receipt>()
            .HasOne(receipt => receipt.Account)
            .WithMany(account => account.Receipts)
            .HasForeignKey(receipt => receipt.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ReceiptItem>()
            .Property(item => item.ProductName)
            .HasMaxLength(150);

        modelBuilder.Entity<ReceiptItem>()
            .Property(item => item.Quantity)
            .HasPrecision(18, 3);

        modelBuilder.Entity<ReceiptItem>()
            .Property(item => item.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ReceiptItem>()
            .Property(item => item.LineTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ReceiptItem>()
            .HasOne(item => item.Receipt)
            .WithMany(receipt => receipt.Items)
            .HasForeignKey(item => item.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Subscription>()
            .Property(subscription => subscription.OwnerUsername)
            .HasMaxLength(100);

        modelBuilder.Entity<Subscription>()
            .Property(subscription => subscription.ServiceName)
            .HasMaxLength(150);

        modelBuilder.Entity<Subscription>()
            .Property(subscription => subscription.ProviderName)
            .HasMaxLength(150);

        modelBuilder.Entity<Subscription>()
            .Property(subscription => subscription.TagsCsv)
            .HasMaxLength(500);

        modelBuilder.Entity<Subscription>()
            .Property(subscription => subscription.CurrencyCode)
            .HasMaxLength(3);

        modelBuilder.Entity<Subscription>()
            .Property(subscription => subscription.Notes)
            .HasMaxLength(500);

        modelBuilder.Entity<Subscription>()
            .Property(subscription => subscription.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Subscription>()
            .HasIndex(subscription => new { subscription.OwnerUserId, subscription.NextDueDateUtc });

        modelBuilder.Entity<Subscription>()
            .HasIndex(subscription => new { subscription.OwnerUserId, subscription.Category });

        modelBuilder.Entity<Subscription>()
            .HasIndex(subscription => new { subscription.OwnerUserId, subscription.Status });
    }
}