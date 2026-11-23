using DisputeDesk.Application.Disputes;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class DisputeReader(DisputeDeskDbContext context) : IDisputeReader, IReviewerDisputeReader
{
    public Task<DisputeReasonOption[]> ListReasonsAsync(CancellationToken cancellationToken) =>
        context.DisputeReasons.AsNoTracking()
            .Where(reason => reason.IsActive)
            .OrderBy(reason => reason.SortOrder)
            .ThenBy(reason => reason.Code)
            .Select(reason => new DisputeReasonOption(reason.Code, reason.Label, reason.RequiresDetails))
            .ToArrayAsync(cancellationToken);

    public Task<DisputeSummary[]> ListAsync(
        string identityUserId, DisputeFilter filter, CancellationToken cancellationToken) =>
        Summaries(Ordered(Filter(identityUserId, filter))).ToArrayAsync(cancellationToken);

    public async Task<DisputePage> PageAsync(string identityUserId, DisputeFilter filter, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var cases = Filter(identityUserId, filter);
        var count = await cases.CountAsync(cancellationToken);
        var lastPage = count == 0 ? 1 : (count - 1) / pageSize + 1;
        page = Math.Min(page, lastPage);
        var items = await Summaries(Ordered(cases).Skip((page - 1) * pageSize).Take(pageSize))
            .ToArrayAsync(cancellationToken);
        return new DisputePage(items, count, page, pageSize);
    }

    private IQueryable<CustomerQueries.OwnedDispute> Filter(string identityUserId, DisputeFilter filter)
    {
        var cases = context.DisputesFor(identityUserId);
        if (filter.Status is { } status)
        {
            cases = cases.Where(item => item.Dispute.Status == status);
        }

        if (filter.From is { } from)
        {
            var start = from.ToUniversalTime();
            cases = cases.Where(item => item.Dispute.SubmittedAt >= start);
        }

        if (filter.To is { } to)
        {
            var end = to.ToUniversalTime();
            cases = cases.Where(item => item.Dispute.SubmittedAt <= end);
        }

        return cases;
    }

    public Task<DisputeDetails?> FindAsync(
        string identityUserId, Guid disputeId, CancellationToken cancellationToken) =>
        FindAsync(context.DisputesFor(identityUserId), disputeId, cancellationToken);

    public Task<DisputeSummary[]> ListOpenAsync(CancellationToken cancellationToken) =>
        Summaries(Ordered(context.DisputesForTransactions(context.Transactions.AsNoTracking())
            .Where(item => item.Dispute.Status == DisputeStatus.Submitted || item.Dispute.Status == DisputeStatus.UnderReview)))
            .ToArrayAsync(cancellationToken);

    public Task<DisputeDetails?> FindAsync(Guid disputeId, CancellationToken cancellationToken) =>
        FindAsync(context.DisputesForTransactions(context.Transactions.AsNoTracking()), disputeId, cancellationToken);

    private static IOrderedQueryable<CustomerQueries.OwnedDispute> Ordered(
        IQueryable<CustomerQueries.OwnedDispute> cases) =>
        cases.OrderByDescending(item => item.Dispute.SubmittedAt).ThenBy(item => item.Dispute.Id);

    private static IQueryable<DisputeSummary> Summaries(IQueryable<CustomerQueries.OwnedDispute> cases) =>
        cases.Select(item => DisputeSummary.From(item.Dispute, item.Transaction));

    private async Task<DisputeDetails?> FindAsync(
        IQueryable<CustomerQueries.OwnedDispute> cases, Guid disputeId, CancellationToken cancellationToken)
    {
        var receipt = await (from item in cases
                             where item.Dispute.Id == disputeId
                             select new
                             {
                                 item.Dispute,
                                 item.Transaction,
                                 History = context.DisputeStatusHistory.AsNoTracking()
                                     .Where(change => change.DisputeId == item.Dispute.Id)
                                     .OrderBy(change => change.ChangedAt)
                                     .ThenBy(change => change.Status == DisputeStatus.Submitted ? 0 : change.Status == DisputeStatus.UnderReview ? 1 : 2)
                                     .ToArray()
                             }).SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? null : DisputeDetails.From(receipt.Dispute, receipt.Transaction, receipt.History);
    }
}
