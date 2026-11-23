namespace DisputeDesk.Application.Transactions;

public interface ITransactionReader
{
    Task<TransactionSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken);
    Task<TransactionPage> PageAsync(string identityUserId, TransactionFilter filter, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<TransactionSummary?> FindAsync(string identityUserId, Guid transactionId, CancellationToken cancellationToken);
}
