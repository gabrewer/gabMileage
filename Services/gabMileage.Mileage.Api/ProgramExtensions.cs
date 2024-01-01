using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using NJsonSchema.Generation.TypeMappers;
using NJsonSchema;
using NSwag;
using NSwag.AspNetCore;
using NSwag.Generation.Processors.Security;
using trakUtility.api;

namespace gabMileage.Mileage.Api;

public static class ProgramExtensions
{
    private const string AppName = "GabMileage - MileageApi";

    public static void AddCustomSwagger(this WebApplicationBuilder builder, SwaggerAuthenticationOptions authenticationOptions)
    {

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddOpenApiDocument(document =>
        {
            document.Version = "v1";
            document.Title = $"{AppName}";
            document.SchemaSettings.AllowReferencesWithProperties = true;
            document.SchemaSettings.GenerateAbstractProperties = true;

            document.AddSecurity("bearer", Enumerable.Empty<string>(), new OpenApiSecurityScheme
            {
                Type = OpenApiSecuritySchemeType.OAuth2,
                Description = "B2C authentication",
                Flow = OpenApiOAuth2Flow.Implicit,
                Flows = new OpenApiOAuthFlows()
                {
                    Implicit = new OpenApiOAuthFlow()
                    {
                        Scopes = DelegatedPermissions.All.ToDictionary(p => $"{authenticationOptions.ApplicationIdUri}/{p}", StringComparer.OrdinalIgnoreCase),
                        AuthorizationUrl = authenticationOptions.AuthorizationUrl,
                        TokenUrl = authenticationOptions.TokenUrl,
                    },
                }
            });

            document.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("bearer"));
        });
    }

    public static void AddCustomAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(options =>
                {
                    builder.Configuration.Bind("AzureAdB2C", options);

                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.NameClaimType = "name";
                },
                options => { builder.Configuration.Bind("AzureAdB2C", options); });
    }

    public static void AddCustomAuthorization(this WebApplicationBuilder builder)
    {
    }

    public static void UseCustomSwagger(this WebApplication app, SwaggerAuthenticationOptions authenticationOptions)
    {
        app.UseOpenApi();
        app.UseSwaggerUi(settings =>
        {
            settings.OAuth2Client = new OAuth2ClientSettings
            {
                ClientId = authenticationOptions.ClientId,
                AppName = authenticationOptions.AppName,
            };
        });
    }
}
