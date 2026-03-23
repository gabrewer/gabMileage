using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace gabMileage.Api.Auth;

// Breadcrumbs (backend-builder)
// What: Authenticate API requests based on the Gateway-forwarded X-User-Id header.
// Why: Keeps JWT/WorkOS validation at the Gateway boundary while still enabling ASP.NET Core authz (401/403) in the Api.
// Alternatives considered: Validate JWT in the Api (rejected: duplicates edge concerns + requires WorkOS/JWT config); allow anonymous + manual checks (rejected: inconsistent 401 behavior).
// Confidence: medium (assumes Api is only reachable behind Gateway in production, per docs).
public sealed class GatewayHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<GatewayForwardedAuthOptions> forwardedAuthOptions,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Keep the literal header name in this file so source-based tests can assert an explicit read pattern.
        if (!Request.Headers.TryGetValue("X-User-Id", out var userIdHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(GatewayHeaderAuthenticationDefaults.UserIdSignatureHeaderName, out var signatureHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        // Reject multi-value identities/signatures (StringValues.ToString() would otherwise comma-join).
        if (userIdHeader.Count != 1 || signatureHeader.Count != 1)
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = (userIdHeader[0] ?? string.Empty).Trim();
        var signature = (signatureHeader[0] ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(signature))
            return Task.FromResult(AuthenticateResult.NoResult());

        // Defense-in-depth: require a shared secret in non-development environments so direct-to-Api header spoofing fails closed.
        var sharedSecret = forwardedAuthOptions.Value.SharedSecret;
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            if (!environment.IsDevelopment())
                return Task.FromResult(AuthenticateResult.NoResult());
        }
        else
        {
            if (!IsValidSignature(sharedSecret, userId, signature))
                return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool IsValidSignature(string sharedSecret, string userId, string signature)
    {
        var secretBytes = Encoding.UTF8.GetBytes(sharedSecret);
        var messageBytes = Encoding.UTF8.GetBytes(userId);

        using var hmac = new HMACSHA256(secretBytes);
        var expected = hmac.ComputeHash(messageBytes);

        byte[] provided;
        try
        {
            provided = Convert.FromBase64String(signature);
        }
        catch
        {
            return false;
        }

        return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(provided, expected);
    }
}
