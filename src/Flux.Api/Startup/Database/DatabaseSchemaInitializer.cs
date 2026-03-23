using System.Data;
using Flux.Data;
using Microsoft.EntityFrameworkCore;

namespace Flux.Api.Startup;

public sealed class DatabaseSchemaInitializer(BankDbContext context) : IDatabaseSchemaInitializer
{
    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDirectory);

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

        EnsureColumnExists(context, "UserAccounts", "Role", "TEXT NOT NULL DEFAULT 'FreeMember'");
        EnsureColumnExists(context, "Accounts", "OwnerUserId", "TEXT NULL");
        EnsureColumnExists(context, "Accounts", "AccountName", "TEXT NULL");
        EnsureColumnExists(context, "Accounts", "CreditCardAprPercent", "REAL NULL");
        EnsureColumnExists(context, "Accounts", "SavingsApyPercent", "REAL NULL");
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
