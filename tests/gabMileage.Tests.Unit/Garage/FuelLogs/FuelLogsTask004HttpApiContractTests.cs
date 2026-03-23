using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class FuelLogsTask004HttpApiContractTests
{
    [Fact]
    public void FuelLogsApi_ShouldProvideCompatibilityAliasRoutes_UnderApiV1Vehicles()
    {
        // Contract (docs/api/fuel-logging.md §3): backend MAY map /api/v1/vehicles/* as strict aliases
        // of canonical /api/v1/garage/vehicles/* routes to avoid drift with early Sprint 03 drafts.
        // Acceptance criteria tie-in: ensures frontend can fetch/record/update/delete even if it still
        // uses the non-canonical route set.
        var program = ReadApiProgramText();

        program.ShouldContain("app.MapGet(\"/api/v1/vehicles/{vehicleId}/fuel-logs\"", Case.Sensitive);
        program.ShouldContain("app.MapPost(\"/api/v1/vehicles/{vehicleId}/fuel-logs\"", Case.Sensitive);
        program.ShouldContain("app.MapPut(\"/api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"", Case.Sensitive);
        program.ShouldContain("app.MapDelete(\"/api/v1/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"", Case.Sensitive);
    }

    [Fact]
    public void UpdateFuelLogRequest_ShouldAllowOptionalVehicleIdAndFuelLogId_InBody()
    {
        // Contract (docs/api/fuel-logging.md PUT): request body may include vehicleId/fuelLogId (optional),
        // and if present must match route values.
        // This test asserts the DTO shape exists to make that validation possible.
        var program = ReadApiProgramText();

        program.ShouldContain("internal sealed record UpdateFuelLogRequest(", Case.Sensitive);
        program.ShouldContain("Guid? VehicleId", Case.Sensitive);
        program.ShouldContain("Guid? FuelLogId", Case.Sensitive);
    }

    [Fact]
    public void UpdateFuelLogEndpoint_ShouldRejectVehicleIdMismatch_BetweenBodyAndRoute()
    {
        // Contract: if vehicleId is present in the body and doesn't match the route, return 400.
        // Important: This must be implemented on the fuel-log PUT endpoint, not (only) on other endpoints.
        var program = ReadApiProgramText();
        var putBlock = ExtractEndpointBlock(
            program,
            "app.MapPut(\"/api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"",
            ".WithName(\"UpdateFuelLog\")");

        putBlock.ShouldContain("request.VehicleId is not null", Case.Sensitive);
        putBlock.ShouldContain("request.VehicleId.Value != vehicleId", Case.Sensitive);
        putBlock.ShouldContain("[\"vehicleId\"] = [\"VehicleId in body must match route.\"]", Case.Sensitive);
    }

    [Fact]
    public void UpdateFuelLogEndpoint_ShouldRejectFuelLogIdMismatch_BetweenBodyAndRoute()
    {
        // Contract: if fuelLogId is present in the body and doesn't match the route, return 400.
        var program = ReadApiProgramText();
        var putBlock = ExtractEndpointBlock(
            program,
            "app.MapPut(\"/api/v1/garage/vehicles/{vehicleId}/fuel-logs/{fuelLogId}\"",
            ".WithName(\"UpdateFuelLog\")");

        putBlock.ShouldContain("request.FuelLogId is not null", Case.Sensitive);
        putBlock.ShouldContain("request.FuelLogId.Value != fuelLogId", Case.Sensitive);
        putBlock.ShouldContain("[\"fuelLogId\"] = [\"FuelLogId in body must match route.\"]", Case.Sensitive);
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
