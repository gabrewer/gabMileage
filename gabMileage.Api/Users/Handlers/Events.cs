namespace gabMileage.Api.Users.Handlers;

// Breadcrumbs (backend-builder)
// What: Minimal "Events" return type for Wolverine handlers.
// Why: Keeps handler signatures expressive (Events.Empty / new Events(e)) while avoiding namespace collisions.
// Alternatives considered: Using JasperFx.Events.Events (not available in current package set).
// Confidence: high
public sealed record Events(params object[] Items)
{
    public static Events Empty { get; } = new();
}
