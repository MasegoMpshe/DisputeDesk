using DisputeDesk.Domain;

namespace DisputeDesk.Application.Disputes;

public interface IDisputeReader
{
    Task<DisputeReasonOption[]> ListReasonsAsync(CancellationToken cancellationToken);
    Task<DisputeSummary[]> ListAsync(string identityUserId, DisputeFilter filter, CancellationToken cancellationToken);
    Task<DisputePage> PageAsync(string identityUserId, DisputeFilter filter, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<DisputeDetails?> FindAsync(string identityUserId, Guid disputeId, CancellationToken cancellationToken);
}

public sealed record DisputeFilter(DisputeStatus? Status, DateTimeOffset? From, DateTimeOffset? To);

public sealed record DisputePage(DisputeSummary[] Items, int TotalCount, int Page, int PageSize);
