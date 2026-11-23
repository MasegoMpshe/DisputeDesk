namespace DisputeDesk.Application.Disputes;

public interface IDisputeNotifications
{
    Task<DisputeNotificationItem[]> ListAsync(string identityUserId, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(string identityUserId, Guid notificationId, CancellationToken cancellationToken);
}

public sealed record DisputeNotificationItem(
    Guid Id, Guid DisputeId, string ReferenceNumber, string Status, DateTimeOffset ChangedAt, DateTimeOffset? ReadAt);
