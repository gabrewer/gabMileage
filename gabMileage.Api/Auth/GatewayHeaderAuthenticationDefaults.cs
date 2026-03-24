namespace gabMileage.Api.Auth;

public static class GatewayHeaderAuthenticationDefaults
{
    public const string SchemeName = "GatewayHeader";

    // Internal contract: Gateway -> Api forwards WorkOS user id as X-User-Id.
    public const string UserIdHeaderName = "X-User-Id";

    // Defense-in-depth: Gateway also forwards an HMAC signature so the Api can reject spoofed headers.
    public const string UserIdSignatureHeaderName = "X-User-Id-Signature";
}
