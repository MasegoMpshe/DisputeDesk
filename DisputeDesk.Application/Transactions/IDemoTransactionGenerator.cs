namespace DisputeDesk.Application.Transactions;

public interface IDemoTransactionGenerator
{
    Task<int> GenerateAsync(string identityUserId, CancellationToken cancellationToken);
}
