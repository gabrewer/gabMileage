using gabMileage.Api.Users.DomainEvents;
using gabMileage.Api.Users.ReadModels;
using JasperFx.Events.Projections;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace gabMileage.Api.Users.Projections;

// SingleStreamProjection<UserProfile>
public sealed class UserProfileProjection : SingleStreamProjection<UserProfile, string>
{
    public UserProfileProjection()
    {
        Lifecycle = ProjectionLifecycle.Inline;
        CreateEvent<UserProvisioned>(Create);
    }

    public UserProfile Create(UserProvisioned e) => new()
    {
        Id = e.UserId,
        DisplayName = e.DisplayName,
        Email = e.Email,
        AvatarUrl = e.AvatarUrl,
        CreatedAt = e.OccurredAt,
    };
}
