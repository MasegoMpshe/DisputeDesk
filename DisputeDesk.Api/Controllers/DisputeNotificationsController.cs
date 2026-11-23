using DisputeDesk.Application.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize(Policy = DisputeAccess.Customer)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/dispute-notifications")]
public sealed class DisputeNotificationsController(IDisputeNotifications notifications) : ControllerBase
{
    [HttpGet]
    public Task<DisputeNotificationItem[]> List(CancellationToken cancellationToken) =>
        notifications.ListAsync(DisputeAccess.UserId(User), cancellationToken);

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken) =>
        await notifications.MarkReadAsync(DisputeAccess.UserId(User), id, cancellationToken)
            ? NoContent()
            : NotFound();
}
