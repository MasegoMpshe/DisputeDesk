namespace DisputeDesk.Domain;

public sealed class Dispute
{
    public static readonly TimeSpan FilingWindow = TimeSpan.FromDays(120);

    public static DateTimeOffset FilingDeadline(DateTimeOffset occurredAt) => occurredAt.ToUniversalTime() + FilingWindow;

    public static bool IsFilingWindowExpired(DateTimeOffset occurredAt, DateTimeOffset now) =>
        now > FilingDeadline(occurredAt);

    private Dispute() { }

    public Guid Id { get; private init; }
    public Guid TransactionId { get; private init; }
    public string ReferenceNumber { get; private init; } = string.Empty;
    public string? ReasonCode { get; private init; }
    public string Reason { get; private init; } = string.Empty;
    public string? Details { get; private init; }
    public DisputeStatus Status { get; private set; } = DisputeStatus.Submitted;
    public DateTimeOffset SubmittedAt { get; private init; }

    public static (Dispute? Dispute, DisputeFilingError? Error) Raise(
        Transaction transaction, DisputeReason? reason, string? details, DateTimeOffset now)
    {
        if (IsFilingWindowExpired(transaction.OccurredAt, now))
        {
            return (null, DisputeFilingError.FilingWindowExpired);
        }

        if (reason is not { IsActive: true })
        {
            return (null, DisputeFilingError.InvalidReason);
        }

        details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        if (reason.RequiresDetails && details is null)
        {
            return (null, DisputeFilingError.DetailsRequired);
        }

        var id = Guid.NewGuid();
        return (new Dispute
        {
            Id = id,
            TransactionId = transaction.Id,
            ReferenceNumber = $"DD-{id:N}".ToUpperInvariant(),
            ReasonCode = reason.Code,
            Reason = reason.Label,
            Details = details,
            SubmittedAt = now
        }, null);
    }

    public DisputeStatusChange SubmissionRecorded(string actorId) =>
        new(Guid.NewGuid(), Id, DisputeStatus.Submitted, actorId, DisputeActor.Customer, SubmittedAt, null);

    public DisputeStatusChange? TryTransition(
        DisputeActor actor, string actorId, DisputeStatus target, string? note, DateTimeOffset now)
    {
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var allowed = (actor, Status, target) switch
        {
            (DisputeActor.Customer, DisputeStatus.Submitted, DisputeStatus.Withdrawn) => true,
            (DisputeActor.Reviewer, DisputeStatus.Submitted, DisputeStatus.UnderReview) => true,
            (DisputeActor.Reviewer, DisputeStatus.UnderReview, DisputeStatus.Approved or DisputeStatus.Rejected) =>
                note is not null,
            _ => false
        };
        if (!allowed)
        {
            return null;
        }

        Status = target;
        return new DisputeStatusChange(Guid.NewGuid(), Id, target, actorId, actor, now, note);
    }
}

public enum DisputeFilingError
{
    FilingWindowExpired,
    InvalidReason,
    DetailsRequired
}
