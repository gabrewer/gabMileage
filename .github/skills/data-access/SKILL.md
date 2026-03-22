---
name: data-access
description: Data access patterns for Lessi.App microservices. Marten event sourcing is the default for all new data. EF Core only for genuinely relational data that benefits from second-level Redis caching. Data types live in the microservice's own feature folders, never in a separate Data project.
invocable: false
tags: [marten, efcore, event-sourcing, cqrs, microservices]
---

# Data Access Patterns

## When to Use This Skill

Use this skill when:
- Designing data access for a new microservice feature
- Choosing between Marten and EF Core for a new entity
- Implementing event sourcing with Marten projections
- Configuring EF Core with second-level caching
- Structuring aggregate roots and their events

## Reference Files

- [marten-event-sourcing.md](marten-event-sourcing.md): Full event sourcing patterns — aggregates, projections, event registration, service setup
- [efcore-relational.md](efcore-relational.md): EF Core with second-level Redis caching — DbContext setup, value converters, when to use EF Core

---

## Decision: Marten vs EF Core

**Default to Marten.** Use EF Core only as the exception.

| Scenario | Technology |
|----------|-----------|
| New features, domain entities, workflows | **Marten** (Event Sourcing) |
| Data that is truly relational (parent/child with FK constraints, complex joins) AND benefits from caching | **EF Core + Second-Level Cache** |
| File metadata, drive tree, reporting views from other services | EF Core (existing pattern) |
| Anything that evolves over time and needs audit history | **Marten** |

**Ask yourself:** "Does this data change over time in ways that matter?" If yes → Marten.

---

## Core Principles

1. **Event Sourcing by Default** — Store what happened, not just current state
2. **Data lives in the microservice** — No separate `*.Data` project; types go in feature folders
3. **Separate read and write concerns** — Use `IQuerySession` for reads, `IDocumentSession` for writes
4. **Internal events vs Public events** — Internal domain events feed projections; public events cross service boundaries via Wolverine/Service Bus
5. **Inline projections** — Always use `ProjectionLifecycle.Inline` for immediate consistency
6. **Never generic repositories** — Purpose-built stores and endpoints only

---

## Feature Folder Structure

Data types, events, and projections all live inside the microservice's feature folder — never in a shared `Data/` project or the `*.Api.Public` contract project (except for types that other services need to read).

```
Services/Lessi.App.YourFeature.Api/
├── YourEntity/                          # Feature folder
│   ├── Endpoints/                       # Minimal API / Wolverine HTTP handlers
│   │   ├── CreateYourEntityEndpoint.cs
│   │   ├── GetYourEntityEndpoint.cs
│   │   └── UpdateYourEntityEndpoint.cs
│   ├── Events/                          # Internal domain events (private to this service)
│   │   └── YourEntityEvents.cs
│   ├── Projections/                     # Marten projections
│   │   └── YourEntityProjection.cs
│   ├── Handlers/                        # Wolverine handlers (event → service bus)
│   │   └── YourEntityEventHandler.cs
│   └── Services/                        # Business logic (optional)
│       └── YourEntityService.cs
├── Extensions/
│   ├── DatabaseAndMessagingExtensions.cs  # Marten + Wolverine registration
│   └── YourFeatureMartenOptions.cs        # All Marten config for this service
└── Program.cs

Services/Lessi.App.YourFeature.Api.Public/  # Only what other services need
├── Contracts/                               # DTOs exposed to callers
│   └── YourEntityDto.cs
├── Events/                                  # Public domain events (cross-service)
│   └── YourEntityPublicEvents.cs
└── Models/                                  # Aggregate models other services query
    └── YourEntityAggregate.cs
```

---

## Marten Quick Reference

### Starting an event stream (creating an aggregate)

```csharp
var id = Ulid.NewUlid().ToGuid();
var created = new YourEntityCreated(id, ...);

session.Events.StartStream<YourEntityAggregate>(id, created);
await session.SaveChangesAsync(ct);
```

### Appending events to an existing stream

```csharp
session.Events.Append(id, new YourEntityUpdated(id, newValue, userId, now));
await session.SaveChangesAsync(ct);
```

### Querying the projected aggregate (read)

```csharp
// Single by Id
var entity = await session.LoadAsync<YourEntityAggregate>(id, ct);

// Query collection
var list = await session.Query<YourEntityAggregate>()
    .Where(e => e.OwnerId == userId)
    .ToListAsync(ct);
```

### Minimal projection (SingleStreamProjection)

```csharp
public class YourEntityProjection : SingleStreamProjection<YourEntityAggregate, Guid>
{
    public YourEntityProjection()
    {
        ProjectEvent<YourEntityCreated>((agg, e) =>
        {
            agg.Id = e.Id;
            agg.Name = e.Name;
            agg.CreatedAt = e.CreatedAt;
            return agg;
        });

        ProjectEvent<YourEntityUpdated>((agg, e) =>
        {
            agg.Name = e.NewName;
            agg.UpdatedAt = e.UpdatedAt;
            return agg;
        });
    }
}
```

See [marten-event-sourcing.md](marten-event-sourcing.md) for full patterns including service registration, event registration, indexes, multi-stream projections, and the internal→public event bridge pattern.

---

## EF Core Quick Reference

Only use EF Core for genuinely relational data. Always configure the second-level cache.

```csharp
// DbContext - always schema-namespaced, with NodaTime converters
public class YourDbContext : DbContext
{
    public YourDbContext(DbContextOptions<YourDbContext> options) : base(options) { }

    public DbSet<YourEntity> YourEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("your_service");
        // Configure entities, indexes, constraints
    }
}

// Registration in Program.cs
builder.AddNpgsqlDataSourceWithEntraId("yourDb");
builder.Services.AddDbContext<YourDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>())
           .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>());
});
builder.AddSecondLevelCache("your-service-cache-");

// Reads - always AsNoTracking, always limit
var items = await _ctx.YourEntities
    .AsNoTracking()
    .Where(e => e.UserId == userId)
    .Take(limit)
    .ToListAsync(ct);
```

See [efcore-relational.md](efcore-relational.md) for full patterns including NodaTime converters, caching setup, and when to use EF Core.

---

## Anti-Patterns to Avoid

| Anti-Pattern | Why | Instead |
|---|---|---|
| Generic repository `IRepository<T>` | Hides query complexity, no limits, can't optimize | Purpose-built endpoints or stores |
| Data types in `*.Data` project | Violates microservice boundary | Feature folder in the service |
| EF Core for event-driven data | Loses audit history | Marten event sourcing |
| Marten for pure relational data with caching needs | No second-level cache support | EF Core + `SecondLevelCacheInterceptor` |
| Application-side joins | O(n×m) memory waste | Joins in SQL or Marten multi-stream projection |
| N+1 queries | Round-trip explosion | Batch with `Include` or multiple `.Query<T>()` calls |
| Unbounded queries | Memory exhaustion | Always `.Take(limit)` |
| Tracking reads in EF Core | Change tracking overhead | `.AsNoTracking()` on all reads |
| `SELECT *` style loads | Fetches unused columns | `.Select()` projection or `IQuerySession.Query<T>()` |
