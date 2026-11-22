using DisputeDesk.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Api;

internal sealed class ProvisionDemoAccounts(
    UserManager<IdentityUser> users,
    DisputeDeskDbContext database,
    IConfiguration configuration)
{
    public async Task ExecuteAsync()
    {
        string[] accountIds = ["demo-naledi", "demo-thabo"];
        var customerCount = await database.Customers.CountAsync(
            customer => accountIds.Contains(customer.IdentityUserId));
        if (customerCount != accountIds.Length)
        {
            throw new InvalidOperationException("Seed the demo customers before provisioning login accounts.");
        }

        var missingAccounts = new List<(string Id, string Password)>();
        foreach (var id in accountIds)
        {
            if (await users.FindByIdAsync(id) is not null)
            {
                continue;
            }

            var password = configuration[$"DemoAccounts:{id}:Password"];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException($"Configure a demo password for {id} before provisioning.");
            }

            missingAccounts.Add((id, password));
        }

        if (missingAccounts.Count == 0)
        {
            return;
        }

        await using var transaction = await database.Database.BeginTransactionAsync();
        foreach (var (id, password) in missingAccounts)
        {
            var user = new IdentityUser { Id = id, UserName = id, LockoutEnabled = true };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create demo account {id}: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
            }
        }

        await transaction.CommitAsync();
    }
}
