# EF Core with Second-Level Caching

## When to Use EF Core

EF Core is the exception, not the default. Only use it when data is:

1. **Genuinely relational** — parent/child FK constraints, complex multi-table joins that Marten document queries can't express cleanly
2. **Cacheable** — the data is read frequently but changes infrequently (e.g. lookup tables, standards, file metadata)
3. **Not event-driven** — no audit history needed, current state is all that matters

**Good EF Core candidates in Lessi.App:** file metadata/drive tree, standards/tags, reporting cross-service views, user settings.

**Bad EF Core candidates:** anything with business workflow, state machines, user-created content, or audit history requirements — use Marten instead.

---

## DbContext Setup

Each service owns exactly one `DbContext`. Name it after the service. Always set `HasDefaultSchema` to match the service's database schema name.

```csharp
// Services/Lessi.App.YourFeature.Api/Data/YourFeatureDbContext.cs

using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Lessi.App.YourFeature.Api.Data;

public class YourFeatureDbContext : DbContext
{
    public YourFeatureDbContext(DbContextOptions<YourFeatureDbContext> options)
        : base(options) { }

    public DbSet<YourEntity> YourEntities { get; set; }
    public DbSet<YourRelatedEntity> YourRelatedEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("yourfeature_service");  // matches Aspire DB resource name

        // Reusable NodaTime converter (define once, use everywhere)
        var instantConverter = new ValueConverter<Instant, DateTime>(
            v => v.ToDateTimeUtc(),
            v => Instant.FromDateTimeUtc(DateTime.SpecifyKind(v, DateTimeKind.Utc))
        );

        var nullableInstantConverter = new ValueConverter<Instant?, DateTime?>(
            v => v.HasValue ? v.Value.ToDateTimeUtc() : null,
            v => v.HasValue
                ? Instant.FromDateTimeUtc(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                : null
        );

        modelBuilder.Entity<YourEntity>(entity =>
        {
            entity.ToTable("your_entities");
            entity.HasKey(e => e.Id);

            // String lengths — always constrain
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2048);

            // NodaTime conversions
            entity.Property(e => e.CreatedAt).HasConversion(instantConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(instantConverter);
            entity.Property(e => e.DeletedAt).HasConversion(nullableInstantConverter);

            // Enum stored as string
            entity.Property(e => e.Status).HasConversion<string>();

            // Indexes for common query patterns
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<YourRelatedEntity>(entity =>
        {
            entity.ToTable("your_related_entities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasConversion(instantConverter);

            // FK relationship
            entity.HasOne(e => e.Parent)
                  .WithMany(p => p.RelatedEntities)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ParentId);
        });
    }
}
```

---

## Entity Definitions

Types live in the service feature folder, not a separate Data project.

```csharp
// Services/Lessi.App.YourFeature.Api/YourEntity/YourEntity.cs

namespace Lessi.App.YourFeature.Api.YourEntity;

// EF Core entities: classes with mutable properties (not records) — EF needs property setters
public class YourEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public YourEntityStatus Status { get; set; }
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
    public Instant? DeletedAt { get; set; }

    // Navigation properties for EF Core relationships
    public List<YourRelatedEntity> RelatedEntities { get; set; } = [];
}

public enum YourEntityStatus { Active, Archived, Deleted }

public class YourRelatedEntity
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Instant CreatedAt { get; set; }

    // Navigation back to parent
    public YourEntity Parent { get; set; } = null!;
}
```

---

## Service Registration

```csharp
// Services/Lessi.App.YourFeature.Api/Program.cs (or an extension method)

// Connect to PostgreSQL via Aspire service discovery
builder.AddNpgsqlDataSourceWithEntraId("yourFeatureDb");

// Register DbContext with the second-level cache interceptor
builder.Services.AddDbContext<YourFeatureDbContext>((serviceProvider, options) =>
{
    options
        .UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>())
        .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>());
});

// Set up Redis-backed second-level cache
// The prefix scopes cache keys to this service — prevents collisions across services
builder.AddSecondLevelCache("yourfeature-cache-");
```

The `AddSecondLevelCache` extension is defined in `Lessi.App.Common.EntityFramework`:

```csharp
// Integration/Lessi.App.Common.EntityFramework/SecondLevelCacheExtensions.cs
// Already registered — just call builder.AddSecondLevelCache("your-prefix-")
```

It:
- Registers `EFCoreSecondLevelCacheInterceptor` with Redis via EasyCaching
- Serializes cache values with MessagePack (with NodaTime support)
- Reads expiration from `appsettings.json: EasyCachingConfig:ExpirationTimeoutMs`
- Calls `CacheAllQueries()` — every query is cached by default

---

## Querying — Read Patterns

**Always `AsNoTracking` for reads.** **Always apply `Take`.** **Always project to DTOs**, not entity types.

```csharp
// List with limit — never return unbounded sets
public async Task<IReadOnlyList<YourEntityDto>> GetByUserAsync(
    string userId,
    int limit,
    CancellationToken ct = default)
{
    var safeLimit = Math.Clamp(limit, 1, 200);

    return await _context.YourEntities
        .AsNoTracking()
        .Where(e => e.UserId == userId && e.DeletedAt == null)
        .OrderByDescending(e => e.CreatedAt)
        .Take(safeLimit)
        .Select(e => new YourEntityDto(e.Id, e.Name, e.Status, e.CreatedAt))
        .ToListAsync(ct);
}

// Single by Id
public async Task<YourEntityDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    return await _context.YourEntities
        .AsNoTracking()
        .Where(e => e.Id == id)
        .Select(e => new YourEntityDto(e.Id, e.Name, e.Status, e.CreatedAt))
        .FirstOrDefaultAsync(ct);
}

// With navigation properties — use Include, never load separately
public async Task<YourEntityWithRelatedDto?> GetWithRelatedAsync(
    Guid id,
    CancellationToken ct = default)
{
    return await _context.YourEntities
        .AsNoTracking()
        .Include(e => e.RelatedEntities)
        .Where(e => e.Id == id)
        .Select(e => new YourEntityWithRelatedDto(
            e.Id,
            e.Name,
            e.RelatedEntities
                .OrderBy(r => r.CreatedAt)
                .Take(50)
                .Select(r => new RelatedDto(r.Id, r.Content))
                .ToList()))
        .FirstOrDefaultAsync(ct);
}
```

---

## Writing — Write Patterns

For writes, use tracked entities. Keep write operations focused.

```csharp
// Create
public async Task<Guid> CreateAsync(CreateYourEntityCommand cmd, CancellationToken ct = default)
{
    var entity = new YourEntity
    {
        Id = Guid.NewGuid(),
        Name = cmd.Name,
        UserId = cmd.UserId,
        Status = YourEntityStatus.Active,
        CreatedAt = SystemClock.Instance.GetCurrentInstant(),
        UpdatedAt = SystemClock.Instance.GetCurrentInstant(),
    };

    _context.YourEntities.Add(entity);
    await _context.SaveChangesAsync(ct);

    return entity.Id;
}

// Update — load, modify, save
public async Task UpdateAsync(Guid id, UpdateYourEntityCommand cmd, CancellationToken ct = default)
{
    var entity = await _context.YourEntities
        .AsTracking()  // explicit tracking for writes
        .FirstOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new KeyNotFoundException($"Entity {id} not found");

    entity.Name = cmd.NewName;
    entity.UpdatedAt = SystemClock.Instance.GetCurrentInstant();

    await _context.SaveChangesAsync(ct);
}

// Soft delete
public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
{
    var entity = await _context.YourEntities
        .AsTracking()
        .FirstOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new KeyNotFoundException($"Entity {id} not found");

    entity.DeletedAt = SystemClock.Instance.GetCurrentInstant();
    entity.Status = YourEntityStatus.Deleted;

    await _context.SaveChangesAsync(ct);
}
```

---

## Opting Out of Caching for Specific Queries

The second-level cache is applied to all queries by default. To bypass it for a specific query (e.g., freshness-sensitive data):

```csharp
var freshData = await _context.YourEntities
    .AsNoTracking()
    .NotCacheable()         // EFCoreSecondLevelCacheInterceptor extension method
    .Where(e => e.UserId == userId)
    .ToListAsync(ct);
```

---

## Avoiding N+1 and Cartesian Explosions

```csharp
// BAD: N+1
var entities = await _context.YourEntities.ToListAsync(ct);
foreach (var e in entities)
{
    var related = await _context.YourRelatedEntities  // N queries!
        .Where(r => r.ParentId == e.Id)
        .ToListAsync(ct);
}

// GOOD: Single query with Include
var entities = await _context.YourEntities
    .AsNoTracking()
    .Include(e => e.RelatedEntities)
    .Take(50)
    .ToListAsync(ct);

// GOOD: Split query when Include would cause Cartesian explosion
// (multiple Includes each with many rows)
var entity = await _context.YourEntities
    .AsNoTracking()
    .AsSplitQuery()
    .Include(e => e.RelatedEntities)
    .Include(e => e.AnotherCollection)
    .FirstOrDefaultAsync(e => e.Id == id, ct);
```

---

## Migration Pattern

Migrations live in `Deploy/migrations/Lessi.App.Migrations`. Never run `dotnet ef migrations add` from inside a service project.

```bash
# Add migration (run from the repo root)
dotnet ef migrations add YourMigrationName \
  --project Deploy/migrations/Lessi.App.Migrations \
  --context YourFeatureDbContext \
  --output-dir Migrations/YourFeature
```

---

## Quick Checklist

When adding EF Core to a service:

- [ ] `DbContext` named after the service (`YourFeatureDbContext`)
- [ ] `HasDefaultSchema("yourfeature_service")` matches DB resource name
- [ ] All `string` properties have `HasMaxLength`
- [ ] All `Instant` properties have NodaTime `ValueConverter`
- [ ] All `enum` properties have `.HasConversion<string>()`
- [ ] All common query predicates have `.HasIndex()`
- [ ] `builder.AddSecondLevelCache("yourfeature-cache-")` registered
- [ ] `AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>())` on `UseNpgsql`
- [ ] All reads use `AsNoTracking()`
- [ ] All list queries have `Take(limit)`
- [ ] Reads project to DTOs, not entity types
