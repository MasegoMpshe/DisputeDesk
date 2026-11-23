namespace DisputeDesk.Application.Disputes;

public interface IReviewerDisputeReader
{
    Task<DisputeSummary[]> ListOpenAsync(CancellationToken cancellationToken);
    Task<DisputeDetails?> FindAsync(Guid disputeId, CancellationToken cancellationToken);
}
