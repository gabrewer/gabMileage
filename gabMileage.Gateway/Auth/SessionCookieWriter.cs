using System.IdentityModel.Tokens.Jwt;

namespace gabMileage.Gateway.Auth;

public static class SessionCookieWriter
{
    public static void SetSessionCookies(HttpContext http, string jwt, string? refreshToken)
    {
        var sessionCookieOptions = BuildSessionCookieOptions(jwt);

        http.Response.Cookies.Append(SessionCookies.GabSession, jwt, sessionCookieOptions);

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            http.Response.Cookies.Append(
                SessionCookies.GabRefresh,
                refreshToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    Expires = sessionCookieOptions.Expires
                });
        }
    }

    public static void ClearSessionCookies(HttpContext http)
    {
        http.Response.Cookies.Delete(SessionCookies.GabSession, new CookieOptions { Path = "/" });
        http.Response.Cookies.Delete(SessionCookies.GabRefresh, new CookieOptions { Path = "/" });
    }

    private static CookieOptions BuildSessionCookieOptions(string jwt)
    {
        DateTimeOffset? expires = null;

        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
            if (token.ValidTo != DateTime.MinValue)
            {
                expires = new DateTimeOffset(token.ValidTo, TimeSpan.Zero);
            }
        }
        catch
        {
            // If parsing fails, still set a session cookie; auth middleware enforces validation.
        }

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expires
        };
    }
}
