namespace gabMileage.Gateway.Auth;

public sealed class WorkOsOptions
{
    // Keep property names aligned with configuration keys under the "WorkOS" section.
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;

    // The absolute callback URL registered in WorkOS.
    public string RedirectUri { get; init; } = string.Empty;

    // Optional: used for more strict JWT validation.
    public string? JwtAuthority { get; init; }
    public string? Audience { get; init; }

    // WorkOS endpoints (configurable to support different WorkOS products/tenants).
    // Note: The docs only require a redirect to https://api.workos.com/... — keep these configurable.
    public string AuthorizationEndpoint { get; init; } = "https://api.workos.com/...";
    public string TokenEndpoint { get; init; } = "https://api.workos.com/...";

    // Refresh-before-expiry skew in seconds.
    public int RefreshSkewSeconds { get; init; } = 300;
}
