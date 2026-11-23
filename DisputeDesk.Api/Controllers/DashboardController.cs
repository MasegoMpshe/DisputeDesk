using System.Security.Claims;
using DisputeDesk.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize(Policy = DisputeAccess.Customer)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/dashboard")]
public sealed class DashboardController(
    ICustomerDashboardReader dashboard, IOptions<DemoTransactionOptions> demoOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CustomerDashboard>> Get(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var summary = await dashboard.GetAsync(userId, cancellationToken);
        return summary with { CanGenerateTransactions = demoOptions.Value.Enabled };
    }
}
