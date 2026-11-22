namespace DisputeDesk.Application.Transactions;

public interface ITransactionReader
{
    Task<TransactionSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken);
    Task<TransactionSummary?> FindAsync(string identityUserId, Guid transactionId, CancellationToken cancellationToken);
}
