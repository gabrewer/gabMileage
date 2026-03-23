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
        // Preserve standard JWT claim names (sub, aud, iss, etc.) so downstream logic can reliably read `sub`.
        options.MapInboundClaims = false;
        var testAuthEnabled = builder.Configuration.GetValue<bool>("GatewayTestAuth:Enabled");
        if (testAuthEnabled && builder.Environment.IsProduction())
        {
            throw new InvalidOperationException("GatewayTestAuth cannot be enabled in Production.");
        }

        // Test auth (non-prod only): validate locally-minted JWTs using a symmetric signing key.
        if (testAuthEnabled)
        {
            var signingKey = builder.Configuration["GatewayTestAuth:SigningKey"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    "GatewayTestAuth:SigningKey is required when GatewayTestAuth:Enabled is true.");
            }

            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.TryGetValue(SessionCookies.GabSession, out var jwt) &&
                        !string.IsNullOrWhiteSpace(jwt))
                    {
                        context.Token = jwt;
                    }

                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
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

            return;
        }

        var workOs = builder.Configuration.GetSection("WorkOS").Get<WorkOsOptions>() ?? new WorkOsOptions();

        // Security: never accept unsigned/unverifiable JWTs. If auth is enabled, WorkOS JWT validation inputs must be present.
        var isConfigured =
            !string.IsNullOrWhiteSpace(workOs.JwtAuthority) &&
            !string.IsNullOrWhiteSpace(workOs.Audience);

        if (!isConfigured)
        {
            if (!builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "WorkOS:JwtAuthority and WorkOS:Audience are required for JWT signature + audience validation.");
            }

            // Development experience: allow the Gateway to serve the anonymous frontend even when WorkOS
            // settings are not yet configured. Auth will be treated as unavailable (all requests unauthenticated).
            options.RequireHttpsMetadata = false;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.NoResult();
                    return Task.CompletedTask;
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
                        Detail = "Authentication required. (WorkOS is not configured in Development.)"
                    };

                    await http.Response.WriteAsync(JsonSerializer.Serialize(problem));
                }
            };

            return;
        }

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
            context.ProxyRequest.Headers.Remove("X-User-Id-Signature");

            // Internal contract: Gateway -> Api forwards WorkOS user ID as X-User-Id derived from JWT `sub` claim.
            // Defense-in-depth: also forward a signature so the Api can reject spoofed headers if it is reachable directly.
            // YARP's RequestTransformContext does not expose route/cluster metadata here, so we scope by path.
            if (context.HttpContext.Request.Path.StartsWithSegments("/api/v1", StringComparison.Ordinal))
            {
                var sub = context.HttpContext.User.FindFirstValue("sub");
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    context.ProxyRequest.Headers.Add("X-User-Id", sub);

                    var secret = builder.Configuration["GatewayForwardedAuth:SharedSecret"] ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                        var sigBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sub));
                        context.ProxyRequest.Headers.Add("X-User-Id-Signature", Convert.ToBase64String(sigBytes));
                    }
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
