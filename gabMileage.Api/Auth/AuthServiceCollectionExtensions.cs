using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace gabMileage.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayForwardedAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddOptions<GatewayForwardedAuthOptions>()
            .BindConfiguration("GatewayForwardedAuth");

        services.AddScoped<AuthContext>(sp =>
        {
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var userId = httpContext?.User.FindFirstValue("sub") ?? string.Empty;
            return new AuthContext(userId);
        });

        services
            .AddAuthentication(GatewayHeaderAuthenticationDefaults.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, GatewayHeaderAuthenticationHandler>(
                GatewayHeaderAuthenticationDefaults.SchemeName,
                _ => { });

        services.AddAuthorization();

        return services;
    }
}
