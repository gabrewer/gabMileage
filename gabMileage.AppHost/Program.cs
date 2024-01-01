using Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var secretStore = builder.AddDaprComponent("gab-secretstore", "secretstores", new
    DaprComponentOptions { LocalPath = "../dapr/components/" });

var apiService = builder.AddProject<Projects.gabMileage_Mileage_Api>("mileage-api")
    .WithDaprSidecar()
    .WithReference(secretStore);

builder.AddProject<Projects.gabMileage_Web>("gabmileage-web")
    .WithDaprSidecar()
    .WithReference(cache);

builder.Build().Run();
