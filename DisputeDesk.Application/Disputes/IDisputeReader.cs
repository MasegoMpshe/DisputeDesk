namespace DisputeDesk.Application.Disputes;

public interface IDisputeReader
{
    Task<DisputeReasonOption[]> ListReasonsAsync(CancellationToken cancellationToken);
    Task<DisputeSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken);
    Task<DisputeDetails?> FindAsync(string identityUserId, Guid disputeId, CancellationToken cancellationToken);
}
