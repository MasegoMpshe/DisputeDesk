using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class DemoTransactionGenerator(DisputeDeskDbContext context, TimeProvider clock) : IDemoTransactionGenerator
{
    private static readonly (string Description, string Merchant, string City, int DaysAgo)[] Purchases =
    [
        ("Grocery purchase", "Corner Basket", "Johannesburg", 0),
        ("Coffee purchase", "Morning Cup", "Pretoria", 7),
        ("Fuel purchase", "Highway Fuel", "Bloemfontein", 35),
        ("Pharmacy purchase", "Neighbourhood Pharmacy", "Cape Town", 125),
        ("Book purchase", "Paper Trail Books", "Durban", 180)
    ];

    public async Task<int> GenerateAsync(string identityUserId, CancellationToken cancellationToken)
    {
        var customerId = await context.Customers
            .Where(customer => customer.IdentityUserId == identityUserId)
            .Select(customer => (Guid?)customer.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        var rows = Purchases.Select(purchase => new Transaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId.Value,
            Description = purchase.Description,
            Merchant = purchase.Merchant,
            City = purchase.City,
            Amount = Random.Shared.Next(2_000, 95_001) / 100m,
            Currency = "ZAR",
            OccurredAt = now.AddDays(-purchase.DaysAgo)
        }).ToArray();

        context.Transactions.AddRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        return rows.Length;
    }
}
