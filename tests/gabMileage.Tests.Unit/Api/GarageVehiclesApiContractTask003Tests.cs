using Shouldly;

namespace gabMileage.Tests.Unit.Api;

public sealed class GarageVehiclesApiContractTask003Tests
{
    [Fact]
    public void GetVehicleEndpoint_ShouldAcceptNonGuidRouteValues_AndReturn400_ForInvalidVehicleId()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);

        // Sprint 02 API contract: invalid vehicleId route value -> 400 Bad Request (ProblemDetails).
        // If the route uses the :guid constraint, invalid GUIDs never hit the handler and typically become 404.
        text.ShouldContain("app.MapGet(\"/api/v1/garage/vehicles/{vehicleId}\"", Case.Sensitive);
        text.ShouldNotContain("/api/v1/garage/vehicles/{vehicleId:guid}", Case.Sensitive);
    }

    [Fact]
    public void UpdateVehicleEndpoint_NotFound_ShouldReturnProblemDetails404_NotBareNotFound()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text, "app.MapPut(\"/api/v1/garage/vehicles/", ".WithName(\"UpdateVehicle\")");

        // Sprint 02 contract: errors are RFC7807 ProblemDetails (application/problem+json), including 404.
        endpointBlock.ShouldContain("Results.Problem", Case.Sensitive);
        endpointBlock.ShouldNotContain("Results.NotFound", Case.Sensitive);
    }

    private static string ExtractBlock(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Could not find marker '{startMarker}'.");

        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, $"Could not find end marker '{endMarker}' after '{startMarker}'.");

        return text.Substring(start, end - start + endMarker.Length);
    }

    private static class RepoRoot
    {
        public static string Find()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, ".git")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate repo root by walking up from AppContext.BaseDirectory.");
        }
    }
}
