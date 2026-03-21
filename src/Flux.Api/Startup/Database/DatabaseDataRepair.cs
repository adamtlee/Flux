using Flux.Data;
using Flux.Data.Models;
using Flux.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace Flux.Api.Startup;

public sealed class DatabaseDataRepair(BankDbContext context) : IDatabaseDataRepair
{
    public async Task RepairDataAsync(CancellationToken cancellationToken = default)
    {
        var firstUser = await context.UserAccounts
            .OrderBy(user => user.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstUser is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(firstUser.Role) || firstUser.Role != ApplicationRoles.Administrator)
        {
            firstUser.Role = ApplicationRoles.Administrator;
        }

        var usersWithoutRole = await context.UserAccounts
            .Where(user => user.Id != firstUser.Id && string.IsNullOrWhiteSpace(user.Role))
            .ToListAsync(cancellationToken);

        foreach (var user in usersWithoutRole)
        {
            user.Role = ApplicationRoles.FreeMember;
        }

        var orphanAccounts = await context.Accounts
            .Where(account => account.OwnerUserId == Guid.Empty)
            .ToListAsync(cancellationToken);

        foreach (var account in orphanAccounts)
        {
            account.OwnerUserId = firstUser.Id;
            if (string.IsNullOrWhiteSpace(account.Owner))
            {
                account.Owner = firstUser.Username;
            }

            account.UpdatedAt = DateTime.UtcNow;
        }

        var accountsMissingName = await context.Accounts
            .Where(account => string.IsNullOrWhiteSpace(account.AccountName))
            .ToListAsync(cancellationToken);

        foreach (var account in accountsMissingName)
        {
            account.AccountName = !string.IsNullOrWhiteSpace(account.Owner)
                ? account.Owner
                : firstUser.Username;
            account.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
