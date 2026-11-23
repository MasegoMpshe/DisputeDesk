using DisputeDesk.Domain;

namespace DisputeDesk.Application.Disputes;

public sealed record DisputeDetails(DisputeSummary Summary, string Reason, string? Details)
{
    public DisputeTimelineEntry[] Timeline { get; init; } = [];

    public static DisputeDetails From(Dispute dispute, Transaction transaction, IEnumerable<DisputeStatusChange> history) =>
        new(DisputeSummary.From(dispute, transaction), dispute.Reason, dispute.Details)
        {
            Timeline = history.Select(change => new DisputeTimelineEntry(
                DisputeSummary.StatusLabel(change.Status), change.Actor.ToString(), change.ChangedAt, change.Note)).ToArray()
        };
}

public sealed record DisputeTimelineEntry(string Status, string Actor, DateTimeOffset ChangedAt, string? Note);
