using NodaTime;

namespace gabMileage.Api.Users.ReadModels;

public sealed class UserProfile
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public Instant CreatedAt { get; set; }
}
