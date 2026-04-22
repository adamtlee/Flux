using System.Data;
using Flux.Data;
using Microsoft.EntityFrameworkCore;

namespace Flux.Api.Startup;

public sealed class DatabaseSchemaInitializer(BankDbContext context) : IDatabaseSchemaInitializer
{
    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        // Ensure the data directory exists for SQLite database file
        // Extract directory from connection string (e.g., "Data Source=data/flux_dev.db" -> "data")
        var connectionString = context.Database.GetConnectionString();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var dataSourceMatch = System.Text.RegularExpressions.Regex.Match(connectionString, @"Data Source=([^;]+)");
            if (dataSourceMatch.Success)
            {
                var dbPath = dataSourceMatch.Groups[1].Value;
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        await context.Database.EnsureCreatedAsync(cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS UserAccounts (
                Id TEXT NOT NULL PRIMARY KEY,
                Username TEXT NOT NULL,
                Role TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                PasswordIterations INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            );
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE UNIQUE INDEX IF NOT EXISTS IX_UserAccounts_Username ON UserAccounts (Username);
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Receipts (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                OwnerUserId TEXT NOT NULL,
                OwnerUsername TEXT NOT NULL,
                AccountId INTEGER NULL,
                MerchantName TEXT NOT NULL,
                PurchasedAtUtc TEXT NOT NULL,
                TotalAmount REAL NOT NULL,
                CurrencyCode TEXT NOT NULL,
                Notes TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT FK_Receipts_Accounts_AccountId FOREIGN KEY (AccountId) REFERENCES Accounts (Id) ON DELETE SET NULL
            );
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ReceiptItems (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ReceiptId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity REAL NOT NULL,
                UnitPrice REAL NOT NULL,
                LineTotal REAL NOT NULL,
                CONSTRAINT FK_ReceiptItems_Receipts_ReceiptId FOREIGN KEY (ReceiptId) REFERENCES Receipts (Id) ON DELETE CASCADE
            );
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_Receipts_OwnerUserId_PurchasedAtUtc ON Receipts (OwnerUserId, PurchasedAtUtc);
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_Receipts_AccountId ON Receipts (AccountId);
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_ReceiptItems_ReceiptId ON ReceiptItems (ReceiptId);
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Subscriptions (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                OwnerUserId TEXT NOT NULL,
                OwnerUsername TEXT NOT NULL,
                ServiceName TEXT NOT NULL,
                ProviderName TEXT NOT NULL,
                Category INTEGER NOT NULL,
                TagsCsv TEXT NOT NULL,
                BillingCycle INTEGER NOT NULL,
                Amount REAL NOT NULL,
                CurrencyCode TEXT NOT NULL,
                StartDateUtc TEXT NOT NULL,
                NextDueDateUtc TEXT NOT NULL,
                ReminderDaysBeforeDue INTEGER NOT NULL,
                AutoRenew INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                Notes TEXT NULL,
                CancelledAtUtc TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_Subscriptions_OwnerUserId_NextDueDateUtc ON Subscriptions (OwnerUserId, NextDueDateUtc);
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_Subscriptions_OwnerUserId_Category ON Subscriptions (OwnerUserId, Category);
        ", cancellationToken);

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_Subscriptions_OwnerUserId_Status ON Subscriptions (OwnerUserId, Status);
        ", cancellationToken);

        EnsureColumnExists(context, "UserAccounts", "Role", "TEXT NOT NULL DEFAULT 'FreeMember'");
        EnsureColumnExists(context, "Accounts", "OwnerUserId", "TEXT NULL");
        EnsureColumnExists(context, "Accounts", "AccountName", "TEXT NULL");
        EnsureColumnExists(context, "Accounts", "CreditCardAprPercent", "REAL NULL");
        EnsureColumnExists(context, "Accounts", "SavingsApyPercent", "REAL NULL");
        EnsureColumnExists(context, "Subscriptions", "ProviderName", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(context, "Subscriptions", "Category", "INTEGER NOT NULL DEFAULT 9");
        EnsureColumnExists(context, "Subscriptions", "TagsCsv", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(context, "Subscriptions", "BillingCycle", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(context, "Subscriptions", "ReminderDaysBeforeDue", "INTEGER NOT NULL DEFAULT 3");
        EnsureColumnExists(context, "Subscriptions", "AutoRenew", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(context, "Subscriptions", "Status", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(context, "Subscriptions", "CancelledAtUtc", "TEXT NULL");
    }

    private static void EnsureColumnExists(BankDbContext context, string tableName, string columnName, string columnDefinition)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";

        var columnExists = false;
        using (var reader = checkCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var existingColumnName = reader.GetString(1);
                if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    columnExists = true;
                    break;
                }
            }
        }

        if (!columnExists)
        {
            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            alterCommand.ExecuteNonQuery();
        }
    }
}
