namespace gabMileage.Api.Auth;

public sealed record GatewayForwardedAuthOptions
{
    public string SharedSecret { get; init; } = string.Empty;
}
