using DisputeDesk.Domain;

namespace DisputeDesk.Application.Disputes;

public interface IDisputeReader
{
    Task<DisputeReasonOption[]> ListReasonsAsync(CancellationToken cancellationToken);
    Task<DisputeSummary[]> ListAsync(string identityUserId, DisputeFilter filter, CancellationToken cancellationToken);
    Task<DisputeDetails?> FindAsync(string identityUserId, Guid disputeId, CancellationToken cancellationToken);
}

public sealed record DisputeFilter(DisputeStatus? Status, DateTimeOffset? From, DateTimeOffset? To);
