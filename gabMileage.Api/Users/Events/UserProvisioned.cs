using NodaTime;

namespace gabMileage.Api.Users.DomainEvents;

public sealed record UserProvisioned(
    string UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    Instant OccurredAt
);
