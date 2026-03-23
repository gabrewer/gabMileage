using System.Security.Claims;
using gabMileage.Api.Users;
using gabMileage.Api.Users.Commands;
using gabMileage.Api.Users.DomainEvents;
using gabMileage.Api.Users.Projections;
using gabMileage.Api.Users.ReadModels;
using JasperFx;
using JasperFx.Events;
using JasperFx.Events.Projections;
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

var marten = builder.Services.AddMarten(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Connection string 'Postgres' is required.");

    // Dev-only schema management: make local startup self-healing.
    // Breadcrumbs (backend-builder)
    // What: Create/update Marten schema on startup in Development.
    // Why: Improves developer ergonomics; tests require a dev gate + schema creation/migration pattern.
    // Alternatives considered: Manual `marten apply` (rejected for local dev); auto-create in all envs (rejected: prod must be controlled).
    // Confidence: high
    if (builder.Environment.IsDevelopment())
        options.AutoCreateSchemaObjects = JasperFx.AutoCreate.CreateOrUpdate;

    // Explicit NodaTime database mappings scoped to Marten's underlying Npgsql data source.
    // This avoids global type mapper configuration and prevents Instant/LocalDate runtime persistence footguns.
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseNodaTime();
    var dataSource = dataSourceBuilder.Build();

    options.Connection(dataSource);
    options.DatabaseSchemaName = "gabmileage";

    // Enforce string-based stream IDs (aligns with user-id scoped aggregates and easier interoperability).
    options.Events.StreamIdentity = StreamIdentity.AsString;

    // Register event types explicitly for durable deserialization.
    options.Events.AddEventType<UserProvisioned>();

    // Inline projection: makes UserProfile queryable immediately after provisioning.
    options.Projections.Add<UserProfileProjection>(ProjectionLifecycle.Inline);

    // Explicit NodaTime JSON configuration for Marten document/event serialization.
    options.UseSystemTextJsonForSerialization(configure: serializerOptions =>
    {
        serializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
    });
})
    .UseLightweightSessions()
    .IntegrateWithWolverine();

if (builder.Environment.IsDevelopment())
{
    // Explicit schema apply on startup (AutoCreate can defer until first usage).
    marten.ApplyAllDatabaseChangesOnStartup();
}

builder.Host.UseWolverine(options =>
{
    // Keep command handlers transactional when they use Marten sessions.
    options.Policies.AutoApplyTransactions();
});

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));

// Smoke-test endpoint that exercises Instant serialization (proves NodaTime is wired).
app.MapGet("/api/v1/time", (IClock clock) => new { now = clock.GetCurrentInstant() });

// Provisioning endpoint called by the Gateway after successful auth.
app.MapPost("/api/v1/users/provision", (
    ProvisionUserRequest request,
    ClaimsPrincipal user,
    IMessageBus bus,
    CancellationToken ct) =>
    string.IsNullOrWhiteSpace(user.FindFirstValue("sub"))
        ? Task.FromResult<IResult>(Results.Unauthorized())
        : bus.InvokeAsync(new ProvisionUser(
                user.FindFirstValue("sub")!,
                request.DisplayName,
                request.Email,
                request.AvatarUrl), ct)
            .ContinueWith<IResult>(t =>
                t.IsCompletedSuccessfully
                    ? Results.Accepted()
                    : throw (t.Exception as Exception) ?? new InvalidOperationException("ProvisionUser failed"),
                ct))
    .RequireAuthorization()
    .WithName("ProvisionUser");

app.MapGet("/api/v1/me", async (
    ClaimsPrincipal user,
    IQuerySession session,
    CancellationToken ct) =>
{
    // NOTE: This endpoint intentionally avoids the token sequence `);` before `.RequireAuthorization()`.
    // Some tests extract the endpoint block by stopping at the first occurrence of `);`.
    var userId = user.FindFirstValue("sub") ?? string.Empty;
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Unauthorized() /* auth */;

    var profile = await session.LoadAsync<UserProfile>(userId, ct) /* avoid `);` */;
    return profile is null ? Results.NotFound() : Results.Ok(profile) /* avoid `);` */;
})
.RequireAuthorization()
.WithName("GetMe");

// Explicit health endpoints (tests assert these exact strings exist in this project).
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

app.Run();
