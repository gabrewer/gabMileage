using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace gabMileage.ServiceDefaults;

internal sealed class StartupHealthCheckValidator(
    HealthCheckService healthCheckService,
    ILogger<StartupHealthCheckValidator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Only validate basic liveness checks at startup.
        // Readiness checks may depend on other services and can deadlock distributed startup.
        var report = await healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Count == 0 || registration.Tags.Contains("live"),
            cancellationToken);

        if (report.Status == HealthStatus.Healthy)
            return;

        var failingChecks = report.Entries
            .Where(x => x.Value.Status != HealthStatus.Healthy)
            .Select(x => $"{x.Key}={x.Value.Status}")
            .ToArray();

        logger.LogError(
            "Host startup aborted because health checks are unhealthy: {FailingChecks}",
            string.Join(", ", failingChecks));

        throw new InvalidOperationException(
            $"Health checks are unhealthy at startup: {string.Join(", ", failingChecks)}");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
