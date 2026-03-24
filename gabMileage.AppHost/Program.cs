using Projects;

// Breadcrumbs (backend-builder)
// What: Aspire AppHost orchestrates local dev resources (Postgres, Redis, Api, Gateway, Web) with explicit env-var mapping.
// Why: Keeps application code portable (no Aspire clients inside apps) and provides production-parity configuration.
// Alternatives considered: Aspire service discovery inside apps (rejected per aspire-configuration skill).
// Confidence: high
//
// Required resources per PRD: gabMileage.Api, gabMileage.Gateway, gabMileage.Web, Postgres, Redis.

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var db = postgres.AddDatabase("gabmileage");

builder.AddRedis("redis");

var api = builder.AddProject<gabMileage_Api>("api")
    .WaitFor(db)
    .WithReference(db, "Postgres")
    .WithHttpHealthCheck("/health");

var web = builder.AddNpmApp("web", "..\\gabMileage.Web", "dev")
    .WithEnvironment("PORT", "3000")
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithHttpHealthCheck("/health");

var gateway = builder.AddProject<gabMileage_Gateway>("gateway")
    // Explicit mapping only: Gateway gets upstream addresses as env vars.
    .WithEnvironment("ReverseProxy__Clusters__api__Destinations__d1__Address", api.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__web__Destinations__d1__Address", web.GetEndpoint("http"))
    .WithHttpHealthCheck("/health");

// TanStack Start executes loaders during SSR; Node's fetch requires absolute URLs.
// Point the Web app at the browser-facing Gateway URL so relative `/api/*` calls work.
web.WithEnvironment("GABMILEAGE_BASE_URL", gateway.GetEndpoint("http"));

builder.Build().Run();
