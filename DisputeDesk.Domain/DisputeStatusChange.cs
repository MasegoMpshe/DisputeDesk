namespace DisputeDesk.Domain;

public enum DisputeActor
{
    Customer,
    Reviewer
}

public sealed record DisputeStatusChange(
    Guid Id,
    Guid DisputeId,
    DisputeStatus Status,
    string ActorId,
    DisputeActor Actor,
    DateTimeOffset ChangedAt,
    string? Note);
