using Microsoft.AspNetCore.Diagnostics.HealthChecks;

// Breadcrumbs (backend-builder)
// What: Configure gabMileage.Gateway as a YARP reverse proxy for Web + Api.
// Why: Centralizes routing so browsers only talk to the Gateway (Web→Gateway→Api).
// Alternatives considered: Serve Web directly from Api (rejected: mixes concerns); direct browser→Api (rejected: future auth edge belongs at gateway).
// Confidence: high

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Explicit health endpoints (tests assert these exact strings exist in this project).
app.MapHealthChecks("/health");
app.MapHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

app.MapReverseProxy();

app.Run();
