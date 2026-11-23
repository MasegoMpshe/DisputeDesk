using DisputeDesk.Domain;

namespace DisputeDesk.Application.Disputes;

public interface IDisputeStore
{
    Task<Transaction?> FindOwnedTransactionAsync(string identityUserId, Guid transactionId, CancellationToken cancellationToken);
    Task<bool> HasDisputeAsync(Guid transactionId, CancellationToken cancellationToken);
    Task<DisputeReason?> FindReasonAsync(string code, CancellationToken cancellationToken);
    Task<bool> InsertAsync(Dispute dispute, DisputeStatusChange submission, CancellationToken cancellationToken);
    Task<DisputeCase?> FindForUpdateAsync(Guid disputeId, string? ownerIdentityId, CancellationToken cancellationToken);
    Task<bool> SaveTransitionAsync(DisputeCase dispute, DisputeStatusChange change, CancellationToken cancellationToken);
}

public sealed record DisputeCase(Dispute Dispute, Transaction Transaction);
