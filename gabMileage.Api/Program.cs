using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;
using Wolverine;
using Wolverine.Marten;

// Breadcrumbs (backend-builder)
// What: Configure gabMileage.Api as a minimal ASP.NET Core service using Marten (event sourcing) + Wolverine (command bus)
// Why: Marten + Wolverine is the default for new domain data; NodaTime establishes consistent time semantics across services.
// Alternatives considered: EF Core (rejected for new features per data-access skill); DateTime/DateTimeOffset (rejected per NodaTime convention).
// Confidence: high

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Time conventions: Instant for timestamps, LocalDate for user-facing dates.
builder.Services.AddSingleton<IClock>(SystemClock.Instance);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
});

builder.Services.AddMarten(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Connection string 'Postgres' is required.");

    // Explicit NodaTime database mappings scoped to Marten's underlying Npgsql data source.
    // This avoids global type mapper configuration and prevents Instant/LocalDate runtime persistence footguns.
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseNodaTime();
    var dataSource = dataSourceBuilder.Build();

    options.Connection(dataSource);
    options.DatabaseSchemaName = "gabmileage";

    // Explicit NodaTime JSON configuration for Marten document/event serialization.
    options.UseSystemTextJsonForSerialization(configure: serializerOptions =>
    {
        serializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
    });

    // Enforce string-based stream IDs (aligns with user-id scoped aggregates and easier interoperability).
    options.Events.StreamIdentity = StreamIdentity.AsString;
})
    .UseLightweightSessions()
    .IntegrateWithWolverine();

builder.Host.UseWolverine(options =>
{
    // Keep command handlers transactional when they use Marten sessions.
    options.Policies.AutoApplyTransactions();
});

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));

// Smoke-test endpoint that exercises Instant serialization (proves NodaTime is wired).
app.MapGet("/api/v1/time", (IClock clock) => new { now = clock.GetCurrentInstant() });

// Explicit health endpoints (tests assert these exact strings exist in this project).
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

app.Run();
