using DisputeDesk.Application.Transactions;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class TransactionReader(DisputeDeskDbContext context) : ITransactionReader
{
    public Task<TransactionSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken) =>
        context.TransactionsFor(identityUserId)
            .NewestFirst()
            .ToSummaries()
            .ToArrayAsync(cancellationToken);

    public Task<TransactionSummary?> FindAsync(
        string identityUserId, Guid transactionId, CancellationToken cancellationToken) =>
        context.TransactionsFor(identityUserId)
            .Where(transaction => transaction.Id == transactionId)
            .ToSummaries()
            .SingleOrDefaultAsync(cancellationToken);
}
