using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class DemoTransactionGenerator(DisputeDeskDbContext context) : IDemoTransactionGenerator
{
    private static readonly (string Description, string Merchant, string City)[] Purchases =
    [
        ("Grocery purchase", "Corner Basket", "Johannesburg"),
        ("Coffee purchase", "Morning Cup", "Pretoria"),
        ("Fuel purchase", "Highway Fuel", "Bloemfontein"),
        ("Pharmacy purchase", "Neighbourhood Pharmacy", "Cape Town"),
        ("Book purchase", "Paper Trail Books", "Durban")
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

        var now = DateTimeOffset.UtcNow;
        var rows = Purchases.Select((purchase, index) => new Transaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId.Value,
            Description = purchase.Description,
            Merchant = purchase.Merchant,
            City = purchase.City,
            Amount = Random.Shared.Next(2_000, 95_001) / 100m,
            Currency = "ZAR",
            OccurredAt = now.AddMilliseconds(-index)
        }).ToArray();

        context.Transactions.AddRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        return rows.Length;
    }
}
