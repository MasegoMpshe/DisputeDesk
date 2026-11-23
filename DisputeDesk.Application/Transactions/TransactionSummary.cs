using DisputeDesk.Domain;

namespace DisputeDesk.Application.Transactions;

public sealed record TransactionSummary(
    Guid Id,
    string Description,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt,
    string? Merchant = null,
    string? City = null)
{
    public DateTimeOffset DisputeDeadline => Dispute.FilingDeadline(OccurredAt);
    public bool FilingWindowExpired { get; init; }

    public TransactionSummary WithFilingWindowAt(DateTimeOffset now) => this with
    {
        FilingWindowExpired = Dispute.IsFilingWindowExpired(OccurredAt, now)
    };
}
