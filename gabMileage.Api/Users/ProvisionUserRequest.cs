namespace gabMileage.Api.Users;

internal sealed record ProvisionUserRequest(
    string DisplayName,
    string Email,
    string? AvatarUrl);
