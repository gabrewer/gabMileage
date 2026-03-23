using Shouldly;

namespace gabMileage.Tests.Unit.Api;

public sealed class GarageVehiclesProblemDetailsLeakDestroyerTests
{
    [Fact]
    public void GetVehicle_NotFoundProblemDetails_MustNotIncludeAuthenticatedUserId_InProblemPayload()
    {
        // Threat model:
        // - 404 for cross-user access is correct, but the ProblemDetails payload must not leak authContext.UserId
        //   (or any tenant identifier) via title/type/detail.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text,
            "app.MapGet(\"/api/v1/garage/vehicles/{vehicleId}\"",
            ".WithName(\"GetVehicle\")");

        var problemBlock = ExtractBlock(endpointBlock, "return Results.Problem(", ");");

        problemBlock.ShouldNotContain("authContext.UserId", Case.Sensitive,
            "ProblemDetails should not include authenticated user identifiers.");
        problemBlock.ShouldNotContain("userId", Case.Insensitive,
            "ProblemDetails should not include authenticated user identifiers.");
    }

    [Fact]
    public void UpdateVehicle_NotFoundProblemDetails_MustNotIncludeAuthenticatedUserId_InProblemPayload()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text,
            "app.MapPut(\"/api/v1/garage/vehicles/{vehicleId}\"",
            ".WithName(\"UpdateVehicle\")");

        var problemBlock = ExtractBlock(endpointBlock, "return Results.Problem(", ");");

        problemBlock.ShouldNotContain("authContext.UserId", Case.Sensitive,
            "ProblemDetails should not include authenticated user identifiers.");
        problemBlock.ShouldNotContain("userId", Case.Insensitive,
            "ProblemDetails should not include authenticated user identifiers.");
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
