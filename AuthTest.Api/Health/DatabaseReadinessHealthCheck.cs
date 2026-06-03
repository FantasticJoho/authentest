using AuthTest.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuthTest.Api.Health;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseReadinessHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _dbContext.Users.AsNoTracking().AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database reachable");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", exception);
        }
    }
}
