using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.Health;

public sealed class DatabaseHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult>
        IHealthCheck.CheckHealthAsync(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext _,
            CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<AuthDbContext>();

            var canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken);

            return canConnect
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult
                    .Healthy("PostgreSQL is reachable.")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult
                    .Unhealthy("PostgreSQL is not reachable.");
        }
        catch (Exception exception)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult
                .Unhealthy(
                    "The PostgreSQL health check failed.",
                    exception);
        }
    }
}
