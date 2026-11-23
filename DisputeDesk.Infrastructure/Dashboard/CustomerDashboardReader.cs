using DisputeDesk.Application.Dashboard;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class CustomerDashboardReader(DisputeDeskDbContext context) : ICustomerDashboardReader
{
    private const int RecentTransactionLimit = 5;

    public async Task<CustomerDashboard> GetAsync(string identityUserId, CancellationToken cancellationToken)
    {
        var transactions = context.TransactionsFor(identityUserId);
        var totalTransactions = await transactions.CountAsync(cancellationToken);
        var counts = await context.DisputesFor(identityUserId)
            .GroupBy(item => item.Dispute.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Count, cancellationToken);
        var disputesByStatus = Enum.GetValues<DisputeStatus>()
            .ToDictionary(status => status, status => counts.GetValueOrDefault(status));
        var recentTransactions = await transactions.NewestFirst()
            .Take(RecentTransactionLimit)
            .ToSummaries()
            .ToArrayAsync(cancellationToken);

        return new CustomerDashboard(totalTransactions, disputesByStatus.Values.Sum(), recentTransactions)
        {
            DisputesByStatus = disputesByStatus
        };
    }
}
