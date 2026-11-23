using DisputeDesk.Application.Disputes;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class DisputeNotificationStore(DisputeDeskDbContext context, TimeProvider time) : IDisputeNotifications
{
    public Task<DisputeNotificationItem[]> ListAsync(string identityUserId, CancellationToken cancellationToken) =>
        (from notification in OwnedNotifications(identityUserId).AsNoTracking()
         join change in context.DisputeStatusHistory.AsNoTracking() on notification.StatusChangeId equals change.Id
         join dispute in context.Disputes.AsNoTracking() on change.DisputeId equals dispute.Id
         orderby change.ChangedAt descending, (change.Status == DisputeStatus.UnderReview ? 0 : 1) descending, notification.StatusChangeId
         select new DisputeNotificationItem(notification.StatusChangeId, dispute.Id, dispute.ReferenceNumber,
             DisputeSummary.StatusLabel(change.Status), change.ChangedAt, notification.ReadAt))
        .ToArrayAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(string identityUserId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = OwnedNotifications(identityUserId).Where(item => item.StatusChangeId == notificationId);
        var readAt = time.GetUtcNow();
        var changed = await notification.Where(item => item.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadAt, readAt), cancellationToken);
        return changed > 0 || await notification.AnyAsync(cancellationToken);
    }

    private IQueryable<DisputeNotification> OwnedNotifications(string identityUserId) =>
        from notification in context.DisputeNotifications
        join customer in context.Customers on notification.CustomerId equals customer.Id
        where customer.IdentityUserId == identityUserId
        select notification;
}
