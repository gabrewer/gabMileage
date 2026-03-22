# Marten Event Sourcing Patterns

## Aggregate Root Definition

Aggregate roots are plain `record` or `class` types with mutable setters — Marten's projection engine sets them. Live in the service's feature folder or the `*.Api.Public` project if other services need to read them.

```csharp
// Services/Lessi.App.YourFeature.Api.Public/Models/YourEntityAggregate.cs
// (in *.Api.Public only if other services query this aggregate directly)

public record YourEntityAggregate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<YourEntityVersion> Versions { get; set; } = [];
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }

    // Helper methods — business logic on the aggregate, not in endpoints
    public YourEntityVersion? GetActiveVersion() =>
        Versions.FirstOrDefault(v => v.IsActive);
}

public record YourEntityVersion
{
    public int VersionNumber { get; set; }
    public bool IsActive { get; set; }
    public string Content { get; set; } = string.Empty;
    public Instant CreatedAt { get; set; }
}
```

---

## Internal Domain Events

Internal events never cross service boundaries. They feed Marten projections. Live in the service's feature folder.

```csharp
// Services/Lessi.App.YourFeature.Api/YourEntity/Events/YourEntityEvents.cs

namespace Lessi.App.YourFeature.Api.YourEntity.Events;

// Naming: past tense, what happened
public record YourEntityCreated(
    Guid EntityId,
    string Name,
    string OwnerId,
    string CreatedBy,
    Instant CreatedAt
);

public record YourEntityRenamed(
    Guid EntityId,
    string NewName,
    string UpdatedBy,
    Instant UpdatedAt
);

public record YourEntityVersionAdded(
    Guid EntityId,
    int VersionNumber,
    string Content,
    string CreatedBy,
    Instant CreatedAt
);

public record YourEntityVersionActivated(
    Guid EntityId,
    int VersionNumber,
    string ActivatedBy,
    Instant ActivatedAt
);

public record YourEntityDeleted(
    Guid EntityId,
    string DeletedBy,
    Instant DeletedAt
);
```

---

## SingleStreamProjection

One aggregate per stream. Always `ProjectionLifecycle.Inline` for immediate read-after-write consistency.

```csharp
// Services/Lessi.App.YourFeature.Api/YourEntity/Projections/YourEntityProjection.cs

using Lessi.App.YourFeature.Api.Public.Models;
using Lessi.App.YourFeature.Api.YourEntity.Events;
using Marten.Events.Projections;

namespace Lessi.App.YourFeature.Api.YourEntity.Projections;

public class YourEntityProjection : SingleStreamProjection<YourEntityAggregate, Guid>
{
    public YourEntityProjection()
    {
        ProjectEvent<YourEntityCreated>((agg, e) =>
        {
            agg.Id = e.EntityId;
            agg.Name = e.Name;
            agg.OwnerId = e.OwnerId;
            agg.Status = "Active";
            agg.Versions = [];
            agg.CreatedAt = e.CreatedAt;
            agg.UpdatedAt = e.CreatedAt;
            agg.CreatedBy = e.CreatedBy;
            agg.IsDeleted = false;
            return agg;
        });

        ProjectEvent<YourEntityRenamed>((agg, e) =>
        {
            agg.Name = e.NewName;
            agg.UpdatedAt = e.UpdatedAt;
            return agg;
        });

        ProjectEvent<YourEntityVersionAdded>((agg, e) =>
        {
            agg.Versions.Add(new YourEntityVersion
            {
                VersionNumber = e.VersionNumber,
                Content = e.Content,
                IsActive = agg.Versions.Count == 0, // auto-activate first version
                CreatedAt = e.CreatedAt,
            });
            agg.UpdatedAt = e.CreatedAt;
            return agg;
        });

        ProjectEvent<YourEntityVersionActivated>((agg, e) =>
        {
            foreach (var v in agg.Versions)
                v.IsActive = v.VersionNumber == e.VersionNumber;
            agg.UpdatedAt = e.ActivatedAt;
            return agg;
        });

        ProjectEvent<YourEntityDeleted>((agg, e) =>
        {
            agg.IsDeleted = true;
            agg.UpdatedAt = e.DeletedAt;
            return agg;
        });
    }
}
```

---

## Endpoint Pattern (Wolverine HTTP)

Events are appended directly in endpoints. Use `IDocumentSession` for writes, `IQuerySession` for reads.

```csharp
// Services/Lessi.App.YourFeature.Api/YourEntity/Endpoints/CreateYourEntityEndpoint.cs

[WolverinePost("/yourfeature/entities")]
public static async Task<Results<Created<YourEntityAggregate>, BadRequest>> Create(
    CreateYourEntityRequest request,
    [NotBody] AppUser user,
    IDocumentSession session,
    CancellationToken ct
)
{
    Guard.Against.NullOrWhiteSpace(request.Name, nameof(request.Name));

    var id = Ulid.NewUlid().ToGuid();
    var now = SystemClock.Instance.GetCurrentInstant();

    var created = new YourEntityCreated(id, request.Name, user.Id, user.Id, now);

    session.Events.StartStream<YourEntityAggregate>(id, created);
    await session.SaveChangesAsync(ct);

    var entity = await session.LoadAsync<YourEntityAggregate>(id, ct);
    return TypedResults.Created($"/yourfeature/entities/{id}", entity);
}

// Read endpoint — IQuerySession is cheaper than IDocumentSession for reads
[WolverineGet("/yourfeature/entities/{id:guid}")]
public static async Task<Results<Ok<YourEntityAggregate>, ProblemHttpResult>> GetById(
    Guid id,
    IQuerySession session,
    CancellationToken ct
)
{
    var entity = await session.LoadAsync<YourEntityAggregate>(id, ct);

    return entity is null
        ? TypedResults.Problem($"Entity {id} not found.", statusCode: 404)
        : TypedResults.Ok(entity);
}

// Update endpoint — append events, never replace whole aggregate
[WolverinePut("/yourfeature/entities/{id:guid}/name")]
public static async Task<Results<Ok, ProblemHttpResult>> Rename(
    Guid id,
    RenameYourEntityRequest request,
    [NotBody] AppUser user,
    IDocumentSession session,
    CancellationToken ct
)
{
    var entity = await session.LoadAsync<YourEntityAggregate>(id, ct);
    if (entity is null)
        return TypedResults.Problem($"Entity {id} not found.", statusCode: 404);

    var now = SystemClock.Instance.GetCurrentInstant();
    session.Events.Append(id, new YourEntityRenamed(id, request.NewName, user.Id, now));
    await session.SaveChangesAsync(ct);

    return TypedResults.Ok();
}

// List endpoint — always apply a limit
[WolverineGet("/yourfeature/entities")]
public static async Task<Ok<IReadOnlyList<YourEntityAggregate>>> List(
    [FromQuery] int limit,
    [NotBody] AppUser user,
    IQuerySession session,
    CancellationToken ct
)
{
    var safeLimit = Math.Clamp(limit, 1, 200);

    var entities = await session.Query<YourEntityAggregate>()
        .Where(e => e.OwnerId == user.Id && !e.IsDeleted)
        .Take(safeLimit)
        .ToListAsync(ct);

    return TypedResults.Ok<IReadOnlyList<YourEntityAggregate>>(entities);
}
```

---

## Internal → Public Event Bridge (Wolverine Handler)

Internal Marten events drive projections. Public events cross service boundaries via the message bus. A Wolverine handler maps between them.

```csharp
// Services/Lessi.App.YourFeature.Api/YourEntity/Handlers/YourEntityEventHandler.cs

using Lessi.App.YourFeature.Api.YourEntity.Events;
using Lessi.App.YourFeature.Api.Public.Events;  // public contract from *.Api.Public
using Wolverine;

namespace Lessi.App.YourFeature.Api.YourEntity.Handlers;

public static class YourEntityEventHandler
{
    // Wolverine auto-discovers Handle methods — no interface needed
    public static async Task Handle(YourEntityCreated @event, IMessageBus bus)
    {
        // Map to public event — only include data other services need
        var publicEvent = new YourEntityWasCreated(
            @event.EntityId,
            @event.Name,
            @event.OwnerId,
            @event.CreatedAt
        );
        await bus.PublishAsync(publicEvent);
    }

    public static async Task Handle(YourEntityRenamed @event, IMessageBus bus)
    {
        await bus.PublishAsync(new YourEntityWasRenamed(
            @event.EntityId,
            @event.NewName,
            @event.UpdatedAt
        ));
    }
}
```

```csharp
// Services/Lessi.App.YourFeature.Api.Public/Events/YourEntityPublicEvents.cs

namespace Lessi.App.YourFeature.Api.Public.Events;

// Public events — minimal surface, stable contract for other services
public record YourEntityWasCreated(
    Guid EntityId,
    string Name,
    string OwnerId,
    Instant CreatedAt
);

public record YourEntityWasRenamed(
    Guid EntityId,
    string NewName,
    Instant UpdatedAt
);
```

---

## Service Registration

All Marten config for a service in one extension method pair: a builder extension and a dedicated options class.

```csharp
// Services/Lessi.App.YourFeature.Api/Extensions/DatabaseAndMessagingExtensions.cs

using Marten;
using Marten.Events.Daemon.Resiliency;
using Weasel.Core;
using Wolverine.Marten;

namespace Lessi.App.YourFeature.Api.Extensions;

public static class DatabaseAndMessagingExtensions
{
    public static WebApplicationBuilder AddDatabaseAndMessaging(
        this WebApplicationBuilder builder)
    {
        builder.AddNpgsqlDataSourceWithEntraId("yourFeatureDb");

        builder
            .Services.AddMartenWithSableSupport(options =>
            {
                YourFeatureMartenOptions.Configure(options);
                options.DisableNpgsqlLogging = true;
                options.OpenTelemetry.TrackConnections = TrackLevel.Normal;
                options.OpenTelemetry.TrackEventCounters();
            })
            .UseLightweightSessions()
            .IntegrateWithWolverine()
            .UseNpgsqlDataSource();

        builder.Services.CritterStackDefaults(x =>
        {
            x.Development.GeneratedCodeMode = TypeLoadMode.Static;
            x.Development.ResourceAutoCreate = AutoCreate.None;
            x.Production.GeneratedCodeMode = TypeLoadMode.Static;
            x.Production.ResourceAutoCreate = AutoCreate.None;
        });

        return builder;
    }
}
```

```csharp
// Services/Lessi.App.YourFeature.Api/Extensions/YourFeatureMartenOptions.cs

using EfCore = Marten.Events.Projections;
using Lessi.App.YourFeature.Api.Public.Models;
using Lessi.App.YourFeature.Api.YourEntity.Events;
using Lessi.App.YourFeature.Api.YourEntity.Projections;
using Marten;
using Marten.Events.Projections;

namespace Lessi.App.YourFeature.Api.Extensions;

public static class YourFeatureMartenOptions
{
    public static void Configure(StoreOptions options)
    {
        options.UseNodaTime();
        options.DatabaseSchemaName = "yourfeature_service";          // one schema per service
        options.UseSystemTextJsonForSerialization(enumStorage: EnumStorage.AsString);

        options.Events.UseArchivedStreamPartitioning = true;          // performance for large event stores

        // Register every event type explicitly — required for deserialization
        options.Events.AddEventType<YourEntityCreated>();
        options.Events.AddEventType<YourEntityRenamed>();
        options.Events.AddEventType<YourEntityVersionAdded>();
        options.Events.AddEventType<YourEntityVersionActivated>();
        options.Events.AddEventType<YourEntityDeleted>();

        // Inline projection — updates the document immediately on SaveChangesAsync
        options.Projections.Add<YourEntityProjection>(ProjectionLifecycle.Inline);

        // Indexes on projected document for common query patterns
        options.Schema.For<YourEntityAggregate>()
            .Index(x => x.OwnerId)
            .Index(x => x.IsDeleted)
            .Index(x => new { x.OwnerId, x.IsDeleted });  // composite for filtered list queries
    }
}
```

---

## Multi-Stream Projection

Use when one document aggregates events from multiple streams (e.g., "all tools assigned to a group").

```csharp
public class GroupEntitySummaryProjection : MultiStreamProjection<GroupEntitySummary, Guid>
{
    public GroupEntitySummaryProjection()
    {
        // Tell Marten which stream(s) each event belongs to
        Identity<YourEntityAssignedToGroup>(e => e.GroupId);
        Identity<YourEntityRemovedFromGroup>(e => e.GroupId);

        ProjectEvent<YourEntityAssignedToGroup>((summary, e) =>
        {
            summary.Id = e.GroupId;
            summary.EntityIds.Add(e.EntityId);
            summary.UpdatedAt = e.AssignedAt;
            return summary;
        });

        ProjectEvent<YourEntityRemovedFromGroup>((summary, e) =>
        {
            summary.EntityIds.Remove(e.EntityId);
            summary.UpdatedAt = e.RemovedAt;
            return summary;
        });
    }
}

public record GroupEntitySummary
{
    public Guid Id { get; set; }           // GroupId
    public List<Guid> EntityIds { get; set; } = [];
    public Instant UpdatedAt { get; set; }
}
```

---

## Marten Document Store (Non-Event-Sourced)

Use `session.Store()` for documents that are snapshots or cache-like data with no history requirement. Still uses Marten, just not event sourcing.

```csharp
// Store a document
session.Store(new YourSnapshotDocument { Id = id, ... });
await session.SaveChangesAsync(ct);

// Load a document
var doc = await session.LoadAsync<YourSnapshotDocument>(id, ct);

// Query documents
var docs = await session.Query<YourSnapshotDocument>()
    .Where(d => d.OwnerId == userId)
    .ToListAsync(ct);

// Delete a document (soft-delete if configured, hard-delete otherwise)
session.Delete<YourSnapshotDocument>(id);
await session.SaveChangesAsync(ct);
```

---

## Common Mistakes

| Mistake | Fix |
|---|---|
| Forgetting `options.Events.AddEventType<T>()` | Every event type must be registered or it won't deserialize after restart |
| Using `ProjectionLifecycle.Async` | Use `Inline` — async requires a daemon and causes eventual consistency |
| Loading aggregate then checking version for optimistic concurrency | Use `session.Events.FetchForWriting<T>()` when you need version-safe writes |
| Querying with `IDocumentSession` for reads | Prefer `IQuerySession` — it's lighter, opens a read-only session |
| Returning the aggregate from an update endpoint | Fetch after save: `await session.LoadAsync<T>(id, ct)` after `SaveChangesAsync` |
| Forgetting `.Index()` on fields used in `.Where()` | Add indexes for all common query predicates in the options class |
