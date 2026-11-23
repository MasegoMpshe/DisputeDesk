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
    ICustomerDashboardReader dashboard, IOptions<DemoTransactionOptions> demoOptions, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CustomerDashboard>> Get(CancellationToken cancellationToken)
    {
        var summary = await dashboard.GetAsync(DisputeAccess.UserId(User), cancellationToken);
        var now = clock.GetUtcNow();
        return summary with
        {
            CanGenerateTransactions = demoOptions.Value.Enabled,
            RecentTransactions = summary.RecentTransactions.Select(item => item.WithFilingWindowAt(now)).ToArray()
        };
    }
}
