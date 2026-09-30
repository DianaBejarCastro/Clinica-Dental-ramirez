using ClinicaRamirez.Api.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClinicaRamirez.Api.Infrastructure;

public sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL no disponible.");
    }
}
