using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Transforms;
using gabMileage.Gateway.Auth;

// Breadcrumbs (backend-builder)
// What: Configure gabMileage.Gateway as a YARP reverse proxy for Web + Api.
// Why: Centralizes routing so browsers only talk to the Gateway (Web→Gateway→Api).
// Alternatives considered: Serve Web directly from Api (rejected: mixes concerns); direct browser→Api (rejected: future auth edge belongs at gateway).
// Confidence: high
//
// Breadcrumbs (backend-builder)
// What: Implement WorkOS hosted auth at the Gateway boundary (login/callback/logout) with a secure HttpOnly cookie.
// Why: Keeps JWT handling out of the frontend and Api; the Gateway is the edge where browser concerns (cookies/redirects) belong.
// Alternatives considered: localStorage tokens (rejected: XSS risk); Api validates JWT directly (rejected: leaks WorkOS concerns into domain service).
// Confidence: high

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDataProtection();

builder.Services.AddOptions<WorkOsOptions>()
    .Bind(builder.Configuration.GetSection("WorkOS"));

builder.Services.AddHttpClient(WorkOsClient.HttpClientName);
builder.Services.AddSingleton<WorkOsClient>();

// The Gateway owns JWT validation. We use JwtBearer so we can protect the proxy pipeline via ASP.NET Core authz.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var workOs = builder.Configuration.GetSection("WorkOS").Get<WorkOsOptions>() ?? new WorkOsOptions();

        // Security: never accept unsigned/unverifiable JWTs. If auth is enabled, WorkOS JWT validation inputs must be present.
        if (string.IsNullOrWhiteSpace(workOs.JwtAuthority))
            throw new InvalidOperationException("WorkOS:JwtAuthority is required for JWT signature validation.");

        if (string.IsNullOrWhiteSpace(workOs.Audience))
            throw new InvalidOperationException("WorkOS:Audience is required for JWT audience validation.");

        options.Authority = workOs.JwtAuthority;
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = workOs.Audience,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Read JWT from the HttpOnly cookie instead of the Authorization header.
                if (context.Request.Cookies.TryGetValue(SessionCookies.GabSession, out var jwt) &&
                    !string.IsNullOrWhiteSpace(jwt))
                {
                    context.Token = jwt;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                // Refresh-before-expiry: if close to exp, attempt refresh using the refresh_token cookie.
                var now = DateTimeOffset.UtcNow;
                var jwt = context.SecurityToken as JwtSecurityToken;
                var expiresUtc = jwt?.ValidTo is { } validTo && validTo != DateTime.MinValue
                    ? new DateTimeOffset(validTo, TimeSpan.Zero)
                    : (DateTimeOffset?)null;

                if (expiresUtc is null)
                    return;

                var refreshSkew = TimeSpan.FromSeconds(workOs.RefreshSkewSeconds);
                if (expiresUtc.Value - now > refreshSkew)
                    return;

                if (!context.HttpContext.Request.Cookies.TryGetValue(SessionCookies.GabRefresh, out var refreshToken) ||
                    string.IsNullOrWhiteSpace(refreshToken))
                {
                    return;
                }

                var client = context.HttpContext.RequestServices.GetRequiredService<WorkOsClient>();

                try
                {
                    var refreshed = await client.RefreshAsync(refreshToken, context.HttpContext.RequestAborted);
                    if (string.IsNullOrWhiteSpace(refreshed.AccessToken))
                        return;

                    SessionCookieWriter.SetSessionCookies(context.HttpContext, refreshed.AccessToken, refreshed.RefreshToken);
                }
                catch
                {
                    // Treat refresh failures as unauthenticated.
                    SessionCookieWriter.ClearSessionCookies(context.HttpContext);
                    context.Fail("refresh_failed");
                }
            },

            OnChallenge = async context =>
            {
                // Ensure XHR/fetch callers get RFC7807 ProblemDetails on 401 per docs/api/foundation-auth.md.
                if (context.Handled)
                    return;

                context.HandleResponse();

                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                http.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.com/401",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "Authentication required."
                };

                await http.Response.WriteAsync(JsonSerializer.Serialize(problem));
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transforms =>
    {
        transforms.AddRequestTransform(context =>
        {
            // Prevent header spoofing from clients.
            context.ProxyRequest.Headers.Remove("X-User-Id");

            // Internal contract: Gateway -> Api forwards WorkOS user ID as X-User-Id derived from JWT `sub` claim.
            // YARP's RequestTransformContext does not expose route/cluster metadata here, so we scope by path.
            if (context.HttpContext.Request.Path.StartsWithSegments("/api/v1", StringComparison.Ordinal))
            {
                var sub = context.HttpContext.User.FindFirstValue("sub");
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    context.ProxyRequest.Headers.Add("X-User-Id", sub);
                }
            }

            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGatewayAuthEndpoints();

// Explicit health endpoints (tests assert these exact strings exist in this project).
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

// Protect the reverse proxy by default; selectively allow anonymous access to the frontend (web cluster).
var proxy = app.MapReverseProxy();
proxy.RequireAuthorization();
proxy.Add(endpointBuilder =>
{
    // Allow anonymous access to the web proxy endpoint (/{**catch-all}).
    if (endpointBuilder is RouteEndpointBuilder route &&
        string.Equals(route.RoutePattern.RawText, "/{**catch-all}", StringComparison.Ordinal))
    {
        endpointBuilder.Metadata.Add(new AllowAnonymousAttribute());
    }
});

app.Run();
