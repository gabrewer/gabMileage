using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace gabMileage.Tests.Unit.ServiceDefaults;

public sealed class ServiceDefaultsObservabilityAndHealthTests
{
    [Fact]
    public void ServiceDefaults_ShouldConfigure_OtlpLogExporter_WhenOtelEndpointIsConfigured()
    {
        var root = RepoRoot.Find();
        var serviceDefaultsDir = Path.Combine(root, "gabMileage.ServiceDefaults");

        Directory.Exists(serviceDefaultsDir)
            .ShouldBeTrue($"Expected ServiceDefaults directory at '{serviceDefaultsDir}'.");

        var csText = ReadAllCsText(serviceDefaultsDir);

        // Acceptance criteria calls out *structured logging defaults* and Aspire visibility.
        // Traces/metrics are exported via OTLP, but logs must also be exportable.
        csText.ShouldContain("builder.Logging.AddOpenTelemetry", Case.Sensitive);
        csText.ShouldContain("logging.AddOtlpExporter", Case.Sensitive);
    }

    [Fact]
    public void ServiceDefaults_ShouldConfigure_OpenTelemetryResource_WithServiceName()
    {
        var root = RepoRoot.Find();
        var serviceDefaultsDir = Path.Combine(root, "gabMileage.ServiceDefaults");

        var csText = ReadAllCsText(serviceDefaultsDir);

        // Without explicit Resource/ServiceName, telemetry can show up inconsistently in dashboards.
        csText.ShouldContain("ConfigureResource", Case.Sensitive);
    }

    [Fact]
    public async Task Host_ShouldNotStart_WhenAnyHealthCheckIsUnhealthy()
    {
        var builder = Host.CreateApplicationBuilder();

        // Under acceptance criteria, services should not start unless basic health checks pass.
        builder.AddServiceDefaults();

        builder.Services.AddHealthChecks()
            .AddCheck("failing", () => HealthCheckResult.Unhealthy("boom"));

        using var host = builder.Build();

        var started = false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await host.StartAsync(cts.Token);

            started = true;
            throw new InvalidOperationException(
                "Host started even though a registered health check is unhealthy. " +
                "Expected ServiceDefaults to enforce startup health validation.");
        }
        catch when (!started)
        {
            // Expected: startup should fail fast when health checks are unhealthy.
        }
        finally
        {
            if (started)
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
    }

    private static string ReadAllCsText(string dir)
    {
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(0, $"Expected at least one .cs file under '{dir}'.");

        return string.Join("\n\n", files.Select(File.ReadAllText));
    }

    private static class RepoRoot
    {
        public static string Find()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, ".git")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate repo root by walking up from AppContext.BaseDirectory.");
        }
    }
}
