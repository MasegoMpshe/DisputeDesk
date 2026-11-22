using DisputeDesk.Application.Disputes;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class DisputeReader(DisputeDeskDbContext context) : IDisputeReader
{
    public Task<DisputeReasonOption[]> ListReasonsAsync(CancellationToken cancellationToken) =>
        context.DisputeReasons.AsNoTracking()
            .Where(reason => reason.IsActive)
            .OrderBy(reason => reason.SortOrder)
            .ThenBy(reason => reason.Code)
            .Select(reason => new DisputeReasonOption(reason.Code, reason.Label, reason.RequiresDetails))
            .ToArrayAsync(cancellationToken);

    public Task<DisputeSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken) =>
        (from owned in context.DisputesFor(identityUserId)
         orderby owned.Dispute.SubmittedAt descending, owned.Dispute.Id
         select new DisputeSummary(
             owned.Dispute.Id, owned.Dispute.TransactionId, owned.Transaction.Description,
             owned.Dispute.ReferenceNumber, owned.Dispute.Status, owned.Dispute.SubmittedAt))
        .ToArrayAsync(cancellationToken);

    public Task<DisputeDetails?> FindAsync(
        string identityUserId, Guid disputeId, CancellationToken cancellationToken) =>
        (from owned in context.DisputesFor(identityUserId)
         where owned.Dispute.Id == disputeId
         select new DisputeDetails(
             new DisputeSummary(
                 owned.Dispute.Id, owned.Dispute.TransactionId, owned.Transaction.Description,
                 owned.Dispute.ReferenceNumber, owned.Dispute.Status, owned.Dispute.SubmittedAt),
             owned.Dispute.Reason,
             owned.Dispute.Details))
        .SingleOrDefaultAsync(cancellationToken);
}
