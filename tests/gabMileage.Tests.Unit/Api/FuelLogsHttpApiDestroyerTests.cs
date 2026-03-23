using Shouldly;

namespace gabMileage.Tests.Unit.Api;

public sealed class FuelLogsHttpApiDestroyerTests
{
    [Fact]
    public void FuelLogsEndpoints_MustNotUseGuidRouteConstraints_SoInvalidGuidsReturn400()
    {
        // Threat model: If routes use {vehicleId:guid}, invalid GUIDs never hit the endpoint and often become 404.
        // Contract requires 400 for invalid ids (ProblemDetails).
        var program = ReadApiProgramText();

        program.ShouldContain("app.MapGet(\"/api/v1/garage/vehicles/{vehicleId}/fuel-logs\"", Case.Sensitive);
        program.ShouldContain("app.MapPost(\"/api/v1/garage/vehicles/{vehicleId}/fuel-logs\"", Case.Sensitive);
        program.ShouldContain("app.MapPut(\"/api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"", Case.Sensitive);
        program.ShouldContain("app.MapDelete(\"/api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"", Case.Sensitive);

        program.ShouldNotContain("/api/v1/garage/vehicles/{vehicleId:guid}/fuel-logs", Case.Sensitive);
        program.ShouldNotContain("/api/v1/garage/vehicles/{vehicleId:guid}/fuel-logs/{fuelLogId}", Case.Sensitive);
        program.ShouldNotContain("/api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId:guid}", Case.Sensitive);
    }

    [Fact]
    public void FuelLogsEndpoints_MustRequireAuthorization_OnCanonicalAndAliasRoutes()
    {
        // Threat model: a missing RequireAuthorization() on any fuel log route enables header spoofing / anonymous access.
        var program = ReadApiProgramText();

        // Canonical routes.
        program.ShouldContain(".WithName(\"GetFuelLogs\")", Case.Sensitive);
        program.ShouldContain(".WithName(\"RecordFuelLog\")", Case.Sensitive);
        program.ShouldContain(".WithName(\"UpdateFuelLog\")", Case.Sensitive);
        program.ShouldContain(".WithName(\"DeleteFuelLog\")", Case.Sensitive);

        // Alias routes are optional, but if present they must also be protected.
        program.ShouldContain("app.MapGet(\"/api/v1/vehicles/{vehicleId}/fuel-logs\"", Case.Sensitive);
        program.ShouldContain("app.MapPost(\"/api/v1/vehicles/{vehicleId}/fuel-logs\"", Case.Sensitive);
        program.ShouldContain("app.MapPut(\"/api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"", Case.Sensitive);
        program.ShouldContain("app.MapDelete(\"/api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"", Case.Sensitive);

        // Signal: RequireAuthorization is applied in the alias section.
        var aliasBlock = ExtractEndpointBlock(program,
            "// Compatibility alias routes",
            "app.MapHealthChecks(\"/health\");");

        aliasBlock.ShouldContain(".RequireAuthorization()", Case.Sensitive);
    }

    [Fact]
    public void GetFuelLogsEndpoint_MustScopeVehicleLookup_ToAuthenticatedUser_AndReturn404ProblemDetails()
    {
        // Threat model: user A guesses user B's vehicleId and attempts to fetch fuel logs.
        // Requirement: behave as 404 without leaking existence.
        var program = ReadApiProgramText();
        var block = ExtractEndpointBlock(
            program,
            "app.MapGet(\"/api/v1/garage/vehicles/{vehicleId}/fuel-logs\"",
            ".WithName(\"GetFuelLogs\")");

        // Must perform an ownership check before returning any list.
        block.ShouldContain("vehicle.UserId", Case.Sensitive);
        block.ShouldContain("authContext.UserId", Case.Sensitive);
        block.ShouldContain("Status404NotFound", Case.Sensitive);
        block.ShouldContain("Results.Problem", Case.Sensitive);

        // Guardrail: do not return Results.NotFound() (contract uses ProblemDetails).
        block.ShouldNotContain("Results.NotFound", Case.Sensitive);
    }

    [Fact]
    public void AliasFuelLogRoutes_ShouldCallSameHandlers_AsCanonicalRoutes()
    {
        // Threat model: alias routes diverge over time and accidentally skip validation/ownership/idempotency logic.
        var program = ReadApiProgramText();
        var aliasBlock = ExtractEndpointBlock(program,
            "// Compatibility alias routes",
            "app.MapHealthChecks(\"/health\");");

        aliasBlock.ShouldContain("RecordFuelLogHandler.Handle", Case.Sensitive);
        aliasBlock.ShouldContain("UpdateFuelLogHandler.Handle", Case.Sensitive);
        aliasBlock.ShouldContain("DeleteFuelLogHandler.Handle", Case.Sensitive);
    }

    private static string ReadApiProgramText()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");
        return File.ReadAllText(path);
    }

    private static string ExtractEndpointBlock(string program, string startSnippet, string endSnippet)
    {
        var start = program.IndexOf(startSnippet, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected Program.cs to contain '{startSnippet}'.");

        var end = program.IndexOf(endSnippet, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, $"Expected Program.cs to contain '{endSnippet}' after '{startSnippet}'.");

        return program.Substring(start, end - start);
    }

    private static class RepoRoot
    {
        public static string Find()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var gitPath = Path.Combine(current.FullName, ".git");
                if (File.Exists(gitPath) || Directory.Exists(gitPath))
                    return current.FullName;

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate repo root by walking up from AppContext.BaseDirectory.");
        }
    }
}
