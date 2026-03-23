# Domain Model — Sprint 01: Foundation & Auth

> **Sprint**: foundation-auth
> **Modeler**: domain-modeler
> **Date**: 2026-03-22
> **Status**: 🔒 Locked — event catalog finalized before builders begin

---

## Overview

Sprint 01 stands up the application skeleton and authentication flow. The domain surface is intentionally narrow: a single aggregate (`User`), one domain event (`UserProvisioned`), and one read model (`UserProfile`). The `GarageSummary` read model is established as an empty-state contract — it carries no events of its own until Sprint 02 introduces vehicles.

---

## 1. Aggregates & Entities

### User

The `User` aggregate represents a gabMileage user provisioned from WorkOS. It exists to record that a WorkOS identity has been seen by the system and to capture their profile at provisioning time.

| Property | Type | Description |
|---|---|---|
| **Id** | `string` | WorkOS user ID (from JWT `sub` claim). Stream key. |
| **DisplayName** | `string` | Display name pulled from WorkOS profile |
| **Email** | `string` | Email address from WorkOS profile |
| **AvatarUrl** | `string?` | Avatar URL from WorkOS (social logins may provide one) |
| **ProvisionedAt** | `Instant` | When the user was first seen by gabMileage |
| **IsProvisioned** | `bool` | Whether provisioning has occurred (guards idempotency) |

**Stream ID**: `user-{WorkOS user ID}` (string-keyed stream — see Decision D-01)

**Invariants**:

1. A user can only be provisioned once. If `UserProvisioned` has already been applied, the `ProvisionUser` command is a no-op (idempotent).
2. `DisplayName` must be non-empty. If WorkOS returns an empty display name, fall back to the email local part.
3. `Email` must be non-empty.

**Aggregate shape** (per Marten conventions):

```csharp
public sealed class User
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public Instant ProvisionedAt { get; set; }
    public bool IsProvisioned { get; set; }

    public void Apply(UserProvisioned e)
    {
        Id = e.UserId;
        DisplayName = e.DisplayName;
        Email = e.Email;
        AvatarUrl = e.AvatarUrl;
        ProvisionedAt = e.OccurredAt;
        IsProvisioned = true;
    }
}
```

---

## 2. Value Objects

This sprint introduces no new value objects. All fields on the `User` aggregate use primitive types or NodaTime types directly.

**Rationale**: The User aggregate has no domain-specific constrained types (no odometer, no fuel amount, no intervals). Introducing value objects for `Email` or `DisplayName` would be premature — these are pass-through values from WorkOS with no gabMileage-specific validation beyond non-emptiness. Value objects will emerge in Sprint 02+ as Vehicle, FuelLog, and MaintenanceItem aggregates are defined.

---

## 3. Event Catalog

### UserProvisioned

Records that a WorkOS user has been seen for the first time by gabMileage and their initial profile has been captured.

```
Event:       UserProvisioned
Stream:      user-{userId}
Stream Type: string-keyed (WorkOS user ID)

Payload:
  - UserId      : string    — WorkOS user ID (JWT sub claim). Primary identifier.
  - DisplayName : string    — Display name from WorkOS profile. Non-empty.
  - Email       : string    — Email address from WorkOS profile. Non-empty.
  - AvatarUrl   : string?   — Avatar URL from WorkOS profile. Nullable.
  - OccurredAt  : Instant   — NodaTime timestamp of when provisioning occurred.

Invariants:
  - UserId must be non-empty
  - DisplayName must be non-empty (fallback to email local part if WorkOS returns empty)
  - Email must be non-empty
  - This event is appended at most once per stream (idempotent provisioning)

Trigger:
  - Gateway auth callback after successful WorkOS authentication, on first login only
```

**C# record definition**:

```csharp
public sealed record UserProvisioned(
    string UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    Instant OccurredAt
);
```

---

## 4. Command Catalog

### ProvisionUser

Provisions a new gabMileage user from WorkOS profile data. Idempotent — if the user has already been provisioned, this command is a no-op.

```
Command:     ProvisionUser
Handler:     ProvisionUserHandler (Wolverine command handler)
Emits:       UserProvisioned (only if user is not yet provisioned)
Stream:      user-{userId}

Payload:
  - UserId      : string    — WorkOS user ID
  - DisplayName : string    — from WorkOS profile
  - Email       : string    — from WorkOS profile
  - AvatarUrl   : string?   — from WorkOS profile

Validation:
  - UserId required and non-empty
  - DisplayName required and non-empty
  - Email required and non-empty

Idempotency:
  - If the User aggregate already has IsProvisioned = true, return without emitting
  - This allows the Gateway to call ProvisionUser on every login without side effects

Caller:
  - Gateway auth callback (POST-authentication, before redirect to dashboard)
```

**C# record definition**:

```csharp
public sealed record ProvisionUser(
    string UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl
);
```

**Handler sketch** (per Marten + Wolverine conventions):

```csharp
public static class ProvisionUserHandler
{
    public static async Task<Events> Handle(
        ProvisionUser command,
        IDocumentSession session,
        IClock clock)
    {
        var existing = await session.Events
            .AggregateStreamAsync<User>(command.UserId);

        if (existing is { IsProvisioned: true })
            return Events.Empty;

        var @event = new UserProvisioned(
            command.UserId,
            command.DisplayName,
            command.Email,
            command.AvatarUrl,
            clock.GetCurrentInstant()
        );

        session.Events.StartStream<User>(command.UserId, @event);
        return Events.Empty;
    }
}
```

---

## 5. Projections

### UserProfileProjection

Builds the `UserProfile` read model from the `UserProvisioned` event. Uses Marten's `SingleStreamProjection` with `Inline` lifecycle for immediate consistency — the read model is available as soon as the event is appended.

```
Projection:    UserProfileProjection
Type:          SingleStreamProjection<UserProfile>
Lifecycle:     Inline
Source Events: UserProvisioned

Read Model:    UserProfile
```

**Read model shape**:

```csharp
public sealed class UserProfile
{
    public string Id { get; set; } = string.Empty;  // WorkOS user ID
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public Instant CreatedAt { get; set; }
}
```

**Projection implementation**:

```csharp
public sealed class UserProfileProjection : SingleStreamProjection<UserProfile>
{
    public UserProfileProjection()
    {
        ProjectionName = "UserProfile";
        Lifecycle = ProjectionLifecycle.Inline;
    }

    public UserProfile Create(UserProvisioned e) => new()
    {
        Id = e.UserId,
        DisplayName = e.DisplayName,
        Email = e.Email,
        AvatarUrl = e.AvatarUrl,
        CreatedAt = e.OccurredAt
    };
}
```

### GarageSummary (Contract Only — No Events This Sprint)

The `GarageSummary` is the dashboard read model. In this sprint it returns an empty vehicle list for every user. It is defined here to establish the API contract; its projection will be implemented in Sprint 02 when `VehicleAdded` events exist.

**Read model shape** (Sprint 01 — minimal):

```csharp
public sealed class GarageSummary
{
    public string UserId { get; set; } = string.Empty;
    public IReadOnlyList<GarageSummaryVehicle> Vehicles { get; set; }
        = Array.Empty<GarageSummaryVehicle>();
}
```

> **Note**: `GarageSummaryVehicle` will be defined in Sprint 02. For Sprint 01, `Vehicles` is always an empty array. The API endpoint can return `new GarageSummary { UserId = authContext.UserId }` directly without a projection.

---

## 6. Auth Infrastructure Contracts (Non-Event-Sourced)

These are not domain aggregates but are essential contracts for this sprint. They are infrastructure concerns implemented at the Gateway and API layers.

### AuthContext

A per-request value extracted from the JWT cookie by the Gateway and forwarded to the API via headers.

```csharp
public sealed record AuthContext(string UserId);
```

**Flow**: Gateway validates `gab_session` cookie → extracts `sub` claim → forwards as `X-User-Id` header → API middleware reads header → `AuthContext` available via DI.

### Session Cookie Contract

| Property | Value |
|---|---|
| Cookie name | `gab_session` |
| HttpOnly | `true` |
| Secure | `true` |
| SameSite | `Lax` |
| Content | JWT from WorkOS |
| Lifetime | Governed by WorkOS token expiry (recommended 7 days) |

---

## 7. Marten Configuration Notes

### Stream Identity

Marten must be configured to support **string-keyed streams** for the `User` aggregate. This is a global Marten setting:

```csharp
services.AddMarten(opts =>
{
    opts.Events.StreamIdentity = StreamIdentity.AsString;
    // ... other config
});
```

> **Important**: This sets string identity for ALL streams, which will also apply to Vehicle, FuelLog, etc. in later sprints. Those aggregates use `Guid` IDs but will format them as strings for stream keys (e.g., `vehicle-{guid}`). This is consistent with the Marten skill's stream naming convention.

### Event Registration

```csharp
opts.Events.AddEventType<UserProvisioned>();
```

### Projection Registration

```csharp
opts.Projections.Add<UserProfileProjection>(ProjectionLifecycle.Inline);
```

---

## 8. Decisions & Rationale

### D-01: String-Keyed Marten Streams

- **Who**: domain-modeler
- **What**: Use `StreamIdentity.AsString` for all Marten event streams, with the `User` aggregate keyed by WorkOS user ID (a string) and future aggregates using `"{aggregate}-{guid}"` format.
- **Why**: The User's canonical identity comes from WorkOS (an external string ID). Introducing a separate Guid and maintaining a mapping adds complexity with no benefit. Marten supports string stream identity natively. Future aggregates (Vehicle, FuelLog) will use the `"{type}-{guid}"` convention (e.g., `vehicle-{guid}`), which is the Marten skill's recommended pattern and works naturally with string stream identity.
- **Alternatives considered**:
  - Guid-only streams with a WorkOS-to-Guid mapping table → rejected: unnecessary indirection, fragile join
  - Mixed stream identity (string for User, Guid for others) → rejected: Marten's `StreamIdentity` is a global setting; mixing requires workarounds
  - Store User as a Marten document (not event-sourced) → rejected: event sourcing provides audit trail and consistency with the rest of the domain
- **Confidence**: High

### D-02: Idempotent User Provisioning

- **Who**: domain-modeler
- **What**: The `ProvisionUser` command is idempotent. If the user stream already contains a `UserProvisioned` event, the command is a no-op.
- **Why**: The Gateway calls `ProvisionUser` on every successful login (it's simpler than checking first). This means the command will be called repeatedly for returning users. Making it idempotent prevents duplicate events and keeps the handler simple — no error path for "user already exists."
- **Alternatives considered**:
  - Gateway checks for existing UserProfile before dispatching → rejected: race condition on first login, adds latency
  - Emit `UserLoggedIn` event on every login → rejected: out of scope for Sprint 01; login tracking is an infrastructure concern, not a domain concern. Can be added later if needed.
- **Confidence**: High

### D-03: No UserUpdated Event in Sprint 01

- **Who**: domain-modeler
- **What**: There is no `UserUpdated` or `UserProfileChanged` event in this sprint. The user profile is set once at provisioning and is immutable for v1.
- **Why**: The sprint brief explicitly states "No user profile editing in v1." WorkOS profile changes (name, avatar) would only be captured on a future re-sync flow, which is out of scope. Adding update events now would be speculative design.
- **Alternatives considered**:
  - Emit `UserUpdated` on every login with latest WorkOS data → rejected: adds complexity, unclear if profile data actually changed, profile editing is explicitly out of scope
  - Define the event now but don't implement → rejected: domain model should reflect what's built, not what might be built
- **Confidence**: High

### D-04: GarageSummary as Direct Query (Not a Projection) in Sprint 01

- **Who**: domain-modeler
- **What**: In Sprint 01, the `GarageSummary` endpoint returns a hardcoded empty response scoped to the authenticated user. No Marten projection backs it yet.
- **Why**: There are no vehicle events in Sprint 01, so there's nothing to project. Defining a projection with no source events is dead code. Sprint 02 will introduce `VehicleAdded` and the corresponding multi-stream projection. The API contract (`GarageSummary` shape) is defined now so the frontend can build against it.
- **Alternatives considered**:
  - Create an empty projection now → rejected: no events to subscribe to, purely dead code
  - Skip the GarageSummary endpoint entirely → rejected: the frontend needs it to render the empty garage state
- **Confidence**: High

### D-05: AuthContext via Header Forwarding (Not Shared JWT Validation)

- **Who**: domain-modeler
- **What**: The Gateway validates the JWT and forwards the user identity to the API as an `X-User-Id` header. The API trusts this header because it's on an internal network (Gateway → API traffic never leaves the Aspire network).
- **Why**: This keeps JWT/WorkOS concerns at the Gateway boundary. The API doesn't need WorkOS SDK dependencies or JWT validation logic. It receives a simple, trusted string. This aligns with the YARP Gateway pattern: Gateway handles auth, backends handle business logic.
- **Alternatives considered**:
  - Forward the full JWT to the API for re-validation → rejected: duplicates validation, requires WorkOS config in the API
  - Shared auth middleware in ServiceDefaults → rejected: only the Gateway should validate external tokens; internal services trust internal headers
- **Confidence**: High

### D-06: NodaTime IClock for Timestamps

- **Who**: domain-modeler
- **What**: All `OccurredAt` timestamps in events are produced by injecting `NodaTime.IClock` (via `SystemClock.Instance` in production) rather than calling `SystemClock.Instance` directly.
- **Why**: Testability. Command handlers and projections that need the current time receive `IClock` via DI, making them deterministic in tests. This is a foundational convention for all future sprints.
- **Alternatives considered**:
  - `SystemClock.Instance.GetCurrentInstant()` directly in handlers → rejected: untestable, time-dependent tests are flaky
  - `DateTime.UtcNow` → rejected: violates NodaTime-only convention
- **Confidence**: High

---

## 9. Sprint Boundary — What's NOT Modeled

The following domain concepts exist in the PRD but are explicitly **out of scope** for this sprint's event catalog:

| Concept | Sprint | Notes |
|---|---|---|
| Vehicle aggregate | Sprint 02 | `VehicleAdded`, `VehicleUpdated`, etc. |
| FuelLog aggregate | Sprint 03 | `FuelLogRecorded`, `FuelLogUpdated`, etc. |
| MaintenanceItem / MaintenanceRecord | Sprint 04 | Maintenance domain events |
| GarageSummary projection | Sprint 02 | Multi-stream projection across vehicles |
| UserUpdated / profile editing | Future | No profile editing in v1 |
| Login audit events | Future | Not a domain concern in v1 |

---

## 10. Event Catalog Summary

| Event | Stream | Trigger | Sprint |
|---|---|---|---|
| `UserProvisioned` | `user-{userId}` | Gateway auth callback (first login) | 01 |

## 11. Command Catalog Summary

| Command | Handler | Emits | Caller |
|---|---|---|---|
| `ProvisionUser` | `ProvisionUserHandler` | `UserProvisioned` | Gateway auth callback |

## 12. Projection Summary

| Projection | Type | Lifecycle | Source Events | Read Model |
|---|---|---|---|---|
| `UserProfileProjection` | SingleStreamProjection | Inline | `UserProvisioned` | `UserProfile` |
