namespace gabMileage.Api.Users.Commands;

public sealed record ProvisionUser(
    string UserId,
    string DisplayName,
    string Email,
    string? AvatarUrl
);
