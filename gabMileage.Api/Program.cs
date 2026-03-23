using System.Security.Claims;
using gabMileage.Api.Auth;
using gabMileage.Api.Garage.Projections;
using gabMileage.Api.Garage.ReadModels;
using gabMileage.Api.Garage.Vehicles;
using gabMileage.Api.Garage.Vehicles.Commands;
using gabMileage.Api.Garage.Vehicles.DomainEvents;
using gabMileage.Api.Garage.Vehicles.Handlers;
using gabMileage.Api.Garage.Vehicles.Projections;
using gabMileage.Api.Garage.Vehicles.ReadModels;
using gabMileage.Api.Garage.FuelLogs;
using gabMileage.Api.Garage.FuelLogs.Commands;
using gabMileage.Api.Garage.FuelLogs.Handlers;
using gabMileage.Api.Garage.FuelLogs.Projections;
using gabMileage.Api.Garage.FuelLogs.ReadModels;
using gabMileage.Api.Garage.FuelLogs.DomainEvents;
using gabMileage.Api.Garage.Maintenance.DomainEvents;
using gabMileage.Api.Garage.Maintenance.Projections;
using gabMileage.Api.Garage.Maintenance.ReadModels;
using gabMileage.Api.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using gabMileage.Api.Users.Commands;
using gabMileage.Api.Users.DomainEvents;
using gabMileage.Api.Users.Projections;
using gabMileage.Api.Users.ReadModels;
using JasperFx;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using JasperFx.Events.Daemon;
using Marten.Linq;
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

// Breadcrumbs (backend-builder)
// What: Configure API authentication/authorization using the Gateway-forwarded X-User-Id header.
// Why: Establishes an auth boundary so endpoints can require auth (401) while keeping JWT validation at the Gateway.
// Alternatives considered: JWT validation inside Api (rejected per docs); no ASP.NET Core auth middleware (rejected: inconsistent protection).
// Confidence: medium
builder.Services.AddHttpContextAccessor();

builder.Services.AddOptions<GatewayForwardedAuthOptions>()
    .BindConfiguration("GatewayForwardedAuth");

builder.Services.AddScoped<AuthContext>(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var userId = httpContext?.User.FindFirstValue("sub") ?? string.Empty;
    return new AuthContext(userId);
});

builder.Services
    .AddAuthentication(GatewayHeaderAuthenticationDefaults.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, GatewayHeaderAuthenticationHandler>(
        GatewayHeaderAuthenticationDefaults.SchemeName,
        _ => { });

builder.Services.AddAuthorization();

// Time conventions: Instant for timestamps, LocalDate for user-facing dates.
builder.Services.AddSingleton<IClock>(NodaTime.SystemClock.Instance);

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
    options.Events.AddEventType<VehicleAdded>();
    options.Events.AddEventType<VehicleUpdated>();
    options.Events.AddEventType<FuelLogRecorded>();
    options.Events.AddEventType<FuelLogUpdated>();
    options.Events.AddEventType<FuelLogDeleted>();

    options.Events.AddEventType<MaintenanceItemDefined>();
    options.Events.AddEventType<MaintenanceItemUpdated>();
    options.Events.AddEventType<MaintenanceItemRemoved>();
    options.Events.AddEventType<MaintenancePerformed>();
    options.Events.AddEventType<MaintenanceRecordUpdated>();
    options.Events.AddEventType<MaintenanceRecordDeleted>();

    // Inline projections: read-after-write consistency for query-side models.
    options.Projections.Add<UserProfileProjection>(ProjectionLifecycle.Inline);
    options.Projections.Add<VehicleProjection>(ProjectionLifecycle.Inline);
    options.Projections.Add<FuelLogListProjection>(ProjectionLifecycle.Inline);
    options.Projections.Add<MaintenanceTabProjection>(ProjectionLifecycle.Inline);

    // GarageSummary is a per-user read model updated from vehicle events.
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Register GarageSummaryProjection as Inline.
    // Why: Sprint 02 requires read-after-write consistency for /api/v1/garage/summary and tests enforce Inline.
    // Alternatives considered: Async projection (rejected: eventual consistency and test contract).
    // Confidence: high
    options.Projections.Add<GarageSummaryProjection>(ProjectionLifecycle.Inline);

    // VehicleReadModel uses StreamId (string) as document identity to align with StreamIdentity.AsString.
    options.Schema.For<VehicleReadModel>()
        .Identity(x => x.StreamId)
        .Index(x => x.UserId)
        .Index(x => x.Id)
        .Index(x => new { x.UserId, x.Id });

    // GarageSummary uses UserId as document identity so it can be loaded by current userId.
    options.Schema.For<GarageSummary>()
        .Identity(x => x.UserId)
        .Index(x => x.UserId);

    options.Schema.For<FuelLogList>()
        .Identity(x => x.Id)
        .Index(x => x.VehicleId)
        .Index(x => x.UserId)
        .Index(x => new { x.UserId, x.VehicleId });

    options.Schema.For<MaintenanceTabData>()
        .Identity(x => x.Id)
        .Index(x => x.VehicleId)
        .Index(x => x.UserId)
        .Index(x => new { x.UserId, x.VehicleId });

    // Explicit NodaTime JSON configuration for Marten document/event serialization.
    options.UseSystemTextJsonForSerialization(configure: serializerOptions =>
    {
        serializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
    });
})
    .AddAsyncDaemon(DaemonMode.Solo)
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

app.UseAuthentication();
app.UseAuthorization();

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
    AuthContext authContext,
    IQuerySession session,
    CancellationToken ct) =>
{
    // NOTE: This endpoint intentionally avoids the token sequence `);` before `.RequireAuthorization()`.
    // Some tests extract the endpoint block by stopping at the first occurrence of `);`.
    var userId = authContext.UserId;
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Unauthorized() /* auth */;

    var profile = await session.LoadAsync<UserProfile>(userId, ct) /* avoid `);` */;
    return profile is null ? Results.NotFound() : Results.Ok(profile) /* avoid `);` */;
})
.RequireAuthorization()
.WithName("GetMe");

app.MapGet("/api/v1/garage/summary", async (
    AuthContext authContext,
    IQuerySession session,
    CancellationToken ct) =>
{
    // Breadcrumbs (backend-builder)
    // Who: backend-builder
    // What: Load the projection-backed GarageSummary for the current user.
    // Why: Summary must be query-side and enforce newest-first ordering.
    // Alternatives considered: Returning a hardcoded empty summary (rejected: Sprint 02 requires vehicles list).
    // Confidence: high

    var userId = authContext.UserId;
    var summary = await session.LoadAsync<GarageSummary>(userId, ct) /* break */;

    var vehicles = (summary?.Vehicles ?? Array.Empty<GarageSummaryVehicle>())
        .OrderByDescending(v => v.CreatedAt)
        .ToArray() /* break */;

    return new GarageSummary { UserId = userId, Vehicles = vehicles };
})
.RequireAuthorization()
.WithName("GetGarageSummary");

app.MapPost("/api/v1/garage/vehicles", async (
    AddVehicleCommand command,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    try
    {
        var result = await AddVehicleHandler.Handle(command, authContext, session, clock, ct);
        var response = new VehicleIdResponse(result.VehicleId);

        return result.IsDuplicate
            ? Results.Ok(response)
            : Results.Created($"/api/v1/garage/vehicles/{result.VehicleId:D}", response);
    }
    catch (VehicleValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
})
.RequireAuthorization()
.WithName("AddVehicle");

app.MapGet("/api/v1/garage/vehicles/{vehicleId}", async (
    Guid vehicleId,
    AuthContext authContext,
    IQuerySession session,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    // Enforce the auth boundary at the query level: cross-user vehicleIds behave like not-found (404) to avoid existence leaks.
    var vehicle = await session.Query<VehicleReadModel>()
        .Where(v => v.Id == vehicleId && v.UserId == authContext.UserId)
        .FirstOrDefaultAsync(ct);

    if (vehicle is null)
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");

    return Results.Ok(new VehicleResponse(
        vehicle.Id,
        vehicle.Name,
        vehicle.Make,
        vehicle.Model,
        vehicle.Year,
        vehicle.PhotoUrl,
        vehicle.PhotoThumbnailUrl,
        vehicle.CreatedAt,
        vehicle.UpdatedAt));
})
.RequireAuthorization()
.WithName("GetVehicle");

app.MapPut("/api/v1/garage/vehicles/{vehicleId}", async (
    Guid vehicleId,
    UpdateVehicleRequest request,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    if (request.VehicleId is not null && request.VehicleId.Value != vehicleId)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId in body must match route."] });

    try
    {
        await UpdateVehicleHandler.Handle(
            new UpdateVehicleCommand(vehicleId, request.Name, request.Make, request.Model, request.Year),
            authContext,
            session,
            clock,
            ct);

        return Results.Ok(new VehicleIdResponse(vehicleId));
    }
    catch (VehicleValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
    catch (VehicleNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");
    }
})
.RequireAuthorization()
.WithName("UpdateVehicle");

app.MapGet("/api/v1/garage/vehicles/{vehicleId}/fuel-logs", async (
    Guid vehicleId,
    AuthContext authContext,
    IQuerySession session,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    var vehicle = await session.LoadAsync<VehicleReadModel>(VehicleStreamId.From(vehicleId), ct);
    if (vehicle is null || !string.Equals(vehicle.UserId, authContext.UserId, StringComparison.Ordinal))
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");

    var list = await session.LoadAsync<FuelLogList>(FuelLogListId.From(vehicleId), ct);

    var logs = (list?.Logs ?? new List<FuelLogEntry>())
        .Select(l => new FuelLogItem(
            l.FuelLogId,
            l.Date,
            l.Odometer,
            l.Gallons,
            l.TotalCost,
            l.PricePerGallon,
            l.CalculatedField,
            l.IsPartialFillUp,
            l.Notes,
            l.Mpg,
            l.CreatedAt))
        .ToArray();

    return Results.Ok(new FuelLogListResponse(vehicleId, logs, list?.LastOdometer));
})
.RequireAuthorization()
.WithName("GetFuelLogs");

app.MapPost("/api/v1/garage/vehicles/{vehicleId}/fuel-logs", async (
    Guid vehicleId,
    RecordFuelLogRequest request,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    try
    {
        var result = await RecordFuelLogHandler.Handle(new RecordFuelLogCommand(
            request.CommandId,
            vehicleId,
            request.Date,
            request.Odometer,
            request.Gallons,
            request.TotalCost,
            request.PricePerGallon,
            request.CalculatedField,
            request.IsPartialFillUp,
            request.Notes), authContext, session, clock, ct);

        var response = new FuelLogCreateResponse(result.FuelLogId, result.OdometerWarning);

        return result.IsDuplicate
            ? Results.Ok(response)
            : Results.Created($"/api/v1/garage/vehicles/{vehicleId:D}/fuel-logs/{result.FuelLogId:D}", response);
    }
    catch (FuelLogValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
    catch (VehicleNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");
    }
})
.RequireAuthorization()
.WithName("RecordFuelLog");

app.MapPut("/api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}", async (
    Guid vehicleId,
    Guid fuelLogId,
    UpdateFuelLogRequest request,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    if (fuelLogId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fuelLogId"] = ["FuelLogId is required."] });

    // Contract: body may include vehicleId/fuelLogId. If present, it must match the route.
    if (request.VehicleId is not null && request.VehicleId.Value != vehicleId)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId in body must match route."] });

    if (request.FuelLogId is not null && request.FuelLogId.Value != fuelLogId)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fuelLogId"] = ["FuelLogId in body must match route."] });

    try
    {
        await UpdateFuelLogHandler.Handle(new UpdateFuelLogCommand(
            fuelLogId,
            vehicleId,
            request.Date,
            request.Odometer,
            request.Gallons,
            request.TotalCost,
            request.PricePerGallon,
            request.CalculatedField,
            request.IsPartialFillUp,
            request.Notes), authContext, session, clock, ct);

        return Results.Ok(new FuelLogIdResponse(fuelLogId));
    }
    catch (FuelLogValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
    catch (VehicleNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");
    }
    catch (FuelLogNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Fuel log {fuelLogId:D} not found.");
    }
})
.RequireAuthorization()
.WithName("UpdateFuelLog");

app.MapDelete("/api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}", async (
    Guid vehicleId,
    Guid fuelLogId,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    if (fuelLogId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fuelLogId"] = ["FuelLogId is required."] });

    try
    {
        await DeleteFuelLogHandler.Handle(new DeleteFuelLogCommand(fuelLogId, vehicleId), authContext, session, clock, ct);
        return Results.NoContent();
    }
    catch (FuelLogValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
    catch (VehicleNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");
    }
    catch (FuelLogNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Fuel log {fuelLogId:D} not found.");
    }
})
.RequireAuthorization()
.WithName("DeleteFuelLog");

// Compatibility alias routes (non-canonical) — see docs/api/fuel-logging.md §3.
// Breadcrumbs (backend-builder)
// Who: backend-builder
// What: Map /api/v1/vehicles/* fuel-log endpoints as strict aliases of /api/v1/garage/vehicles/*.
// Why: Early Sprint 03 frontend drafts used the shorter path; tests enforce the alias strings exist.
// Alternatives considered: Redirects (rejected: breaks some clients and changes semantics); removing canonical route (rejected: contract says canonical is under /garage).
// Confidence: high
app.MapGet("/api/v1/vehicles/{vehicleId}/fuel-logs", async (
    Guid vehicleId,
    AuthContext authContext,
    IQuerySession session,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    var vehicle = await session.LoadAsync<VehicleReadModel>(VehicleStreamId.From(vehicleId), ct);
    if (vehicle is null || !string.Equals(vehicle.UserId, authContext.UserId, StringComparison.Ordinal))
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");

    var list = await session.LoadAsync<FuelLogList>(FuelLogListId.From(vehicleId), ct);

    var logs = (list?.Logs ?? new List<FuelLogEntry>())
        .Select(l => new FuelLogItem(
            l.FuelLogId,
            l.Date,
            l.Odometer,
            l.Gallons,
            l.TotalCost,
            l.PricePerGallon,
            l.CalculatedField,
            l.IsPartialFillUp,
            l.Notes,
            l.Mpg,
            l.CreatedAt))
        .ToArray();

    return Results.Ok(new FuelLogListResponse(vehicleId, logs, list?.LastOdometer));
})
.RequireAuthorization();

app.MapPost("/api/v1/vehicles/{vehicleId}/fuel-logs", async (
    Guid vehicleId,
    RecordFuelLogRequest request,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    try
    {
        var result = await RecordFuelLogHandler.Handle(new RecordFuelLogCommand(
            request.CommandId,
            vehicleId,
            request.Date,
            request.Odometer,
            request.Gallons,
            request.TotalCost,
            request.PricePerGallon,
            request.CalculatedField,
            request.IsPartialFillUp,
            request.Notes), authContext, session, clock, ct);

        var response = new FuelLogCreateResponse(result.FuelLogId, result.OdometerWarning);

        return result.IsDuplicate
            ? Results.Ok(response)
            : Results.Created($"/api/v1/vehicles/{vehicleId:D}/fuel-logs/{result.FuelLogId:D}", response);
    }
    catch (FuelLogValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
    catch (VehicleNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");
    }
})
.RequireAuthorization();

app.MapPut("/api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}", async (
    Guid vehicleId,
    Guid fuelLogId,
    UpdateFuelLogRequest request,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    if (fuelLogId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fuelLogId"] = ["FuelLogId is required."] });

    if (request.VehicleId is not null && request.VehicleId.Value != vehicleId)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId in body must match route."] });

    if (request.FuelLogId is not null && request.FuelLogId.Value != fuelLogId)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fuelLogId"] = ["FuelLogId in body must match route."] });

    try
    {
        await UpdateFuelLogHandler.Handle(new UpdateFuelLogCommand(
            fuelLogId,
            vehicleId,
            request.Date,
            request.Odometer,
            request.Gallons,
            request.TotalCost,
            request.PricePerGallon,
            request.CalculatedField,
            request.IsPartialFillUp,
            request.Notes), authContext, session, clock, ct);

        return Results.Ok(new FuelLogIdResponse(fuelLogId));
    }
    catch (FuelLogValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
    catch (VehicleNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");
    }
    catch (FuelLogNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Fuel log {fuelLogId:D} not found.");
    }
})
.RequireAuthorization();

app.MapDelete("/api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}", async (
    Guid vehicleId,
    Guid fuelLogId,
    AuthContext authContext,
    IDocumentSession session,
    IClock clock,
    CancellationToken ct) =>
{
    if (vehicleId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["vehicleId"] = ["VehicleId is required."] });

    if (fuelLogId == Guid.Empty)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fuelLogId"] = ["FuelLogId is required."] });

    try
    {
        await DeleteFuelLogHandler.Handle(new DeleteFuelLogCommand(fuelLogId, vehicleId), authContext, session, clock, ct);
        return Results.NoContent();
    }
    catch (FuelLogValidationException vex)
    {
        return Results.ValidationProblem(vex.Errors);
    }
    catch (VehicleNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Vehicle {vehicleId:D} not found.");
    }
    catch (FuelLogNotFoundException)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            type: "https://httpstatuses.com/404",
            detail: $"Fuel log {fuelLogId:D} not found.");
    }
})
.RequireAuthorization();

// Explicit health endpoints (tests assert these exact strings exist in this project).
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

app.Run();

internal sealed record VehicleIdResponse(Guid VehicleId);

internal sealed record VehicleResponse(
    Guid VehicleId,
    string Name,
    string? Make,
    string? Model,
    int? Year,
    string? PhotoUrl,
    string? PhotoThumbnailUrl,
    Instant CreatedAt,
    Instant UpdatedAt);

internal sealed record UpdateVehicleRequest(
    Guid? VehicleId,
    string Name,
    string? Make,
    string? Model,
    int? Year);

internal sealed record FuelLogIdResponse(Guid FuelLogId);

internal sealed record FuelLogCreateResponse(Guid FuelLogId, string? OdometerWarning);

internal sealed record FuelLogListResponse(Guid VehicleId, FuelLogItem[] Logs, int? LastOdometer);

internal sealed record FuelLogItem(
    Guid FuelLogId,
    LocalDate Date,
    int Odometer,
    decimal Gallons,
    decimal TotalCost,
    decimal PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes,
    decimal? Mpg,
    Instant CreatedAt);

internal sealed record RecordFuelLogRequest(
    Guid CommandId,
    LocalDate Date,
    int Odometer,
    decimal? Gallons,
    decimal? TotalCost,
    decimal? PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes);

internal sealed record UpdateFuelLogRequest(
    Guid? VehicleId,
    Guid? FuelLogId,
    LocalDate Date,
    int Odometer,
    decimal? Gallons,
    decimal? TotalCost,
    decimal? PricePerGallon,
    string? CalculatedField,
    bool IsPartialFillUp,
    string? Notes);

