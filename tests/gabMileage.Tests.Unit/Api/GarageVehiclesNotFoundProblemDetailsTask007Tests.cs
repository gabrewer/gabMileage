using Shouldly;

namespace gabMileage.Tests.Unit.Api;

public sealed class GarageVehiclesNotFoundProblemDetailsTask007Tests
{
    [Fact]
    public void GetVehicleEndpoint_NotFoundOrCrossUser_ShouldReturnProblemDetails404_WithExplicitTypeAndTitle()
    {
        // Contract: 404 for missing OR cross-user access, using RFC7807 ProblemDetails.
        // This test enforces explicit metadata so clients can reliably classify errors.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text,
            "app.MapGet(\"/api/v1/garage/vehicles/{vehicleId}\"",
            ".WithName(\"GetVehicle\")");

        endpointBlock.ShouldContain("StatusCodes.Status404NotFound", Case.Sensitive);

        // Require explicit title/type for determinism.
        endpointBlock.ShouldContain("title:", Case.Sensitive);
        endpointBlock.ShouldContain("type:", Case.Sensitive);
    }

    [Fact]
    public void UpdateVehicleEndpoint_NotFoundOrCrossUser_ShouldReturnProblemDetails404_WithExplicitTypeAndTitle()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text,
            "app.MapPut(\"/api/v1/garage/vehicles/{vehicleId}\"",
            ".WithName(\"UpdateVehicle\")");

        endpointBlock.ShouldContain("StatusCodes.Status404NotFound", Case.Sensitive);
        endpointBlock.ShouldContain("title:", Case.Sensitive);
        endpointBlock.ShouldContain("type:", Case.Sensitive);
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
