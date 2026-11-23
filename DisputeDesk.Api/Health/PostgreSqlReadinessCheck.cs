using DisputeDesk.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DisputeDesk.Api.Health;

internal sealed class PostgreSqlReadinessCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var database = services.GetRequiredService<DisputeDeskDbContext>();
        var pending = await database.Database.GetPendingMigrationsAsync(cancellationToken);

        return pending.Any()
            ? HealthCheckResult.Unhealthy("Database migrations are pending.")
            : HealthCheckResult.Healthy();
    }
}
