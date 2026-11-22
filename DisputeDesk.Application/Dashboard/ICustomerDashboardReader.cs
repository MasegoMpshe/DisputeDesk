namespace DisputeDesk.Application.Dashboard;

public interface ICustomerDashboardReader
{
    Task<CustomerDashboard> GetAsync(string identityUserId, CancellationToken cancellationToken);
}
