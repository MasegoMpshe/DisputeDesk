using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;

namespace DisputeDesk.Application.Dashboard;

public sealed record CustomerDashboard(
    int TotalTransactionCount,
    int TotalDisputes,
    TransactionSummary[] RecentTransactions,
    bool CanGenerateTransactions = false)
{
    public IReadOnlyDictionary<DisputeStatus, int> DisputesByStatus { get; init; } =
        new Dictionary<DisputeStatus, int>();
}
