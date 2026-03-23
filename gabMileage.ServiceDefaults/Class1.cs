using gabMileage.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>
    /// Adds common gabMileage defaults: OpenTelemetry, health checks,
    /// and HTTP resilience.
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Breadcrumbs (backend-builder)
        // What: Centralize observability + health behavior in a single shared project.
        // Why: Guarantees consistent telemetry (incl. service name) + health behavior across all services.
        // Alternatives considered: Per-service duplication in each Program.cs (rejected: drifts over time).
        // Confidence: high

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        // Breadcrumbs (backend-builder)
        // What: Fail-fast startup if any registered health check is unhealthy.
        // Why: Acceptance criteria + unit tests expect services not to start in an unhealthy state.
        // Alternatives considered: Relying on external probes only (rejected: failure is too late/noisy).
        // Confidence: high
        builder.Services.AddHostedService<StartupHealthCheckValidator>();

        // Breadcrumbs (backend-builder)
        // What: Standard HttpClient resilience defaults.
        // Why: Gives sane timeouts/retries for outbound calls (e.g., Gateway→Api).
        // Alternatives considered: Per-client custom policies everywhere (rejected: inconsistent).
        // Confidence: medium
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;

            // Breadcrumbs (backend-builder)
            // What: Enable OTLP exporting for logs when Aspire provides an OTLP endpoint.
            // Why: Acceptance criteria calls out structured logging defaults + Aspire visibility.
            // Alternatives considered: Console-only logs (rejected: not correlated with traces).
            // Confidence: high
            if (useOtlp)
            {
                logging.AddOtlpExporter();
            }
        });

        builder.Services.AddOpenTelemetry()
            // Breadcrumbs (backend-builder)
            // What: Explicit OpenTelemetry Resource service name.
            // Why: Prevents inconsistent naming in dashboards.
            // Alternatives considered: Defaults (rejected: can be ambiguous across hosts).
            // Confidence: high
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                            !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters(useOtlp);

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder, bool useOtlp)
        where TBuilder : IHostApplicationBuilder
    {
        if (useOtlp)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddOtlpExporter())
                .WithMetrics(metrics => metrics.AddOtlpExporter());
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps health check endpoints.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
