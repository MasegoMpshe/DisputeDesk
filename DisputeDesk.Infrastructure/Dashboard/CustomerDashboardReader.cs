using DisputeDesk.Application.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class CustomerDashboardReader(DisputeDeskDbContext context) : ICustomerDashboardReader
{
    private const int RecentTransactionLimit = 5;

    public async Task<CustomerDashboard> GetAsync(string identityUserId, CancellationToken cancellationToken)
    {
        var transactions = context.TransactionsFor(identityUserId);
        var totalTransactions = await transactions.CountAsync(cancellationToken);
        var totalDisputes = await context.DisputesFor(identityUserId).CountAsync(cancellationToken);
        var recentTransactions = await transactions.NewestFirst()
            .Take(RecentTransactionLimit)
            .ToSummaries()
            .ToArrayAsync(cancellationToken);

        return new CustomerDashboard(totalTransactions, totalDisputes, recentTransactions);
    }
}
