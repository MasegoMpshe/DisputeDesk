using DisputeDesk.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Api;

internal sealed class ProvisionDemoAccounts(
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
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

        await using var transaction = await database.Database.BeginTransactionAsync();
        foreach (var id in accountIds)
        {
            if (await users.FindByIdAsync(id) is null)
            {
                var password = configuration[$"DemoAccounts:{id}:Password"];
                if (string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException($"Configure a demo password for {id} before provisioning.");
                }

                await CreateUserAsync(id, password);
            }
        }

        const string reviewerId = "demo-reviewer";
        var reviewerPassword = configuration[$"DemoAccounts:{reviewerId}:Password"];
        if (!string.IsNullOrWhiteSpace(reviewerPassword))
        {
            if (!await roles.RoleExistsAsync(DisputeAccess.Reviewer))
            {
                EnsureSuccess(await roles.CreateAsync(new IdentityRole(DisputeAccess.Reviewer)), "create the reviewer role");
            }

            var reviewer = await users.FindByIdAsync(reviewerId) ?? await CreateUserAsync(reviewerId, reviewerPassword);
            if (!await users.IsInRoleAsync(reviewer, DisputeAccess.Reviewer))
            {
                EnsureSuccess(await users.AddToRoleAsync(reviewer, DisputeAccess.Reviewer), "assign the reviewer role");
            }
        }

        await transaction.CommitAsync();
    }

    private async Task<IdentityUser> CreateUserAsync(string id, string password)
    {
        var user = new IdentityUser { Id = id, UserName = id, LockoutEnabled = true };
        EnsureSuccess(await users.CreateAsync(user, password), $"create demo account {id}");
        return user;
    }

    private static void EnsureSuccess(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
