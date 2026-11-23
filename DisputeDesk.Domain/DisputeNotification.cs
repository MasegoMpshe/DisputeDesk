namespace DisputeDesk.Domain;

public sealed class DisputeNotification(Guid statusChangeId, Guid customerId)
{
    public Guid StatusChangeId { get; private init; } = statusChangeId;
    public Guid CustomerId { get; private init; } = customerId;
    public DateTimeOffset? ReadAt { get; private set; }
}
