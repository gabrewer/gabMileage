using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace gabMileage.Gateway.Auth;

public static class GatewayAuthEndpoints
{
    public static IEndpointRouteBuilder MapGatewayAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/auth/login
        app.MapGet("/api/auth/login", (
                [FromQuery] string? returnTo,
                HttpContext http,
                IDataProtectionProvider dataProtection,
                WorkOsClient workos) =>
            {
                // Validate returnTo is a safe relative URL.
                var safeReturnTo = ValidateReturnToOrDefault(returnTo);
                if (safeReturnTo is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["returnTo"] = ["Must be a relative path (no scheme/host)."]
                    });
                }

                var nonce = Guid.NewGuid().ToString("N");
                http.Response.Cookies.Append(
                    SessionCookies.GabAuthNonce,
                    nonce,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        Expires = DateTimeOffset.UtcNow.AddMinutes(10)
                    });

                var protector = dataProtection.CreateProtector("gabMileage.Gateway.WorkOS.state");
                var statePayload = $"returnTo={safeReturnTo}&nonce={nonce}";
                var state = WebEncoders.Base64UrlEncode(
                    System.Text.Encoding.UTF8.GetBytes(protector.Protect(statePayload)));

                var url = workos.BuildAuthorizeUrl(state);
                return Results.Redirect(url);
            })
            .AllowAnonymous();

        // GET /api/auth/callback
        app.MapGet("/api/auth/callback", async (
                [FromQuery] string? code,
                [FromQuery] string? state,
                HttpContext http,
                IDataProtectionProvider dataProtection,
                WorkOsClient workos,
                IOptionsMonitor<JwtBearerOptions> bearerOptions,
                CancellationToken ct) =>
            {
                var wantsJson = http.Request.Headers.TryGetValue("Accept", out var accept) &&
                    accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(code))
                {
                    return wantsJson
                        ? Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request",
                            detail: "Missing 'code'.")
                        : Results.Redirect("/auth/error?code=auth_failed");
                }

                try
                {
                    // State is required to prevent login CSRF/session swapping and to carry returnTo.
                    if (string.IsNullOrWhiteSpace(state))
                        throw new SecurityTokenException("Missing state.");

                    var protector = dataProtection.CreateProtector("gabMileage.Gateway.WorkOS.state");
                    var protectedPayload = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(state));
                    var payload = protector.Unprotect(protectedPayload);

                    var parsed = QueryHelpers.ParseQuery(payload);

                    var safeReturnTo = "/";
                    if (parsed.TryGetValue("returnTo", out var rt))
                    {
                        safeReturnTo = ValidateReturnToOrDefault(rt.ToString()) ?? "/";
                    }

                    if (!parsed.TryGetValue("nonce", out var nonceFromState) ||
                        string.IsNullOrWhiteSpace(nonceFromState.ToString()))
                    {
                        throw new SecurityTokenException("Invalid state nonce.");
                    }

                    if (!http.Request.Cookies.TryGetValue(SessionCookies.GabAuthNonce, out var nonceCookie) ||
                        !string.Equals(nonceCookie, nonceFromState.ToString(), StringComparison.Ordinal))
                    {
                        throw new SecurityTokenException("Invalid state nonce.");
                    }

                    // Nonce is single-use.
                    http.Response.Cookies.Delete(SessionCookies.GabAuthNonce, new CookieOptions { Path = "/" });

                    var tokenResponse = await workos.ExchangeCodeAsync(code, ct);
                    var jwt = tokenResponse.AccessToken;

                    // Validate JWT using the same parameters as the JwtBearer pipeline.
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var parameters = bearerOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
                    _ = handler.ValidateToken(jwt, parameters, out _);

                    // Cookie contract from docs/api + docs/domain
                    SessionCookieWriter.SetSessionCookies(http, jwt, tokenResponse.RefreshToken);

                    // Callback success must redirect to the frontend loading screen route.
                    return Results.Redirect($"/auth/redirect?returnTo={Uri.EscapeDataString(safeReturnTo)}");
                }
                catch (Exception ex) when (ex is SecurityTokenException or InvalidOperationException or HttpRequestException)
                {
                    // Clear cookies on failure (best-effort).
                    SessionCookieWriter.ClearSessionCookies(http);
                    http.Response.Cookies.Delete(SessionCookies.GabAuthNonce, new CookieOptions { Path = "/" });

                    return wantsJson
                        ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                            detail: "Authentication required.")
                        : Results.Redirect("/auth/error?code=auth_failed");
                }
            })
            .AllowAnonymous();

        // POST /api/auth/logout
        app.MapPost("/api/auth/logout", (HttpContext http) =>
            {
                // Contract: clear the gab_session cookie and redirect to /login using 303 See Other.
                SessionCookieWriter.ClearSessionCookies(http);
                http.Response.Cookies.Delete(SessionCookies.GabSession, new CookieOptions { Path = "/" });
                return new SeeOtherResult("/login");
            })
            .AllowAnonymous();

        return app;
    }

    private static string? ValidateReturnToOrDefault(string? returnTo)
    {
        if (string.IsNullOrWhiteSpace(returnTo))
            return "/";

        // Defense-in-depth: decode before validating to catch encoded scheme-relative redirects (e.g. /%2f%2fevil.com).
        var candidate = returnTo.Trim();
        for (var i = 0; i < 2; i++)
        {
            var decoded = Uri.UnescapeDataString(candidate);
            if (string.Equals(decoded, candidate, StringComparison.Ordinal))
                break;
            candidate = decoded;
        }

        // Safe relative URL validation per contract.
        if (!Uri.TryCreate(candidate, UriKind.Relative, out _))
            return null;

        if (!candidate.StartsWith("/", StringComparison.Ordinal))
            return null;

        if (candidate.StartsWith("//", StringComparison.Ordinal))
            return null;

        if (candidate.Contains('\\'))
            return null;

        return candidate;
    }
}
