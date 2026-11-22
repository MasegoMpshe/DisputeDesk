using DisputeDesk.Application.Transactions;

namespace DisputeDesk.Application.Dashboard;

public sealed record CustomerDashboard(
    int TotalTransactionCount,
    int TotalDisputes,
    TransactionSummary[] RecentTransactions,
    bool CanGenerateTransactions = false);
