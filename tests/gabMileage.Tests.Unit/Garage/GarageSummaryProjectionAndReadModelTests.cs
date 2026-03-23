using System.Text;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage;

public sealed class GarageSummaryProjectionAndReadModelTests
{
    [Fact]
    public void GarageSummary_ReadModel_ShouldReplace_Sprint01Placeholder_WithExpectedVehicleFields()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "ReadModels", "GarageSummary.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Contract from docs/api/garage-vehicles.md and docs/domain/garage-vehicles.md
        text.ShouldContain("class GarageSummary", Case.Sensitive);
        text.ShouldContain("class GarageSummaryVehicle", Case.Sensitive);

        // Vehicles list should no longer be a placeholder element type
        text.ShouldContain("Guid VehicleId", Case.Sensitive);
        text.ShouldContain("string Name", Case.Sensitive);
        text.ShouldContain("string? Make", Case.Sensitive);
        text.ShouldContain("string? Model", Case.Sensitive);
        text.ShouldContain("int? Year", Case.Sensitive);

        // Photo fields are placeholders in Sprint 02 (always null)
        text.ShouldContain("string? PhotoUrl", Case.Sensitive);
        text.ShouldContain("string? PhotoThumbnailUrl", Case.Sensitive);

        // createdAt is required on summary cards
        text.ShouldContain("Instant CreatedAt", Case.Sensitive);
    }

    [Fact]
    public void GarageSummaryProjection_ShouldExist_AsMultiStreamProjection_AndMaintainNewestFirstOrdering()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Must be a real Marten projection (multi-stream) per domain model.
        csText.ShouldContain("class GarageSummaryProjection", Case.Sensitive);
        csText.ShouldContain("MultiStreamProjection<GarageSummary", Case.Sensitive);
        csText.ShouldContain("ProjectionLifecycle.Inline", Case.Sensitive);

        // Acceptance criteria: vehicles sorted by createdAt desc (newest first).
        // Domain model suggests inserting new vehicles at index 0 OR ordering by CreatedAt desc at query time.
        var hasNewestFirstOrdering =
            csText.Contains("Vehicles.Insert(0", StringComparison.Ordinal) ||
            csText.Contains("OrderByDescending", StringComparison.Ordinal);

        hasNewestFirstOrdering.ShouldBeTrue(
            "GarageSummary must return vehicles newest-first (descending createdAt). Implement via Insert(0, ...) in the projection or OrderByDescending at query time.");
    }

    [Fact]
    public void Api_MartenConfiguration_ShouldRegister_GarageSummaryProjection_AsInlineProjection()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var martenBlock = ExtractBlock(text, "AddMarten(options =>", "})");

        // The read model must be projection-backed for /api/v1/garage/summary.
        martenBlock.ShouldContain("GarageSummaryProjection", Case.Sensitive);
        martenBlock.ShouldContain("ProjectionLifecycle.Inline", Case.Sensitive);
    }

    [Fact]
    public void GetGarageSummaryEndpoint_ShouldLoadProjectedSummary_AndReturnVehiclesNewestFirst()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text, "app.MapGet(\"/api/v1/garage/summary\"", ".WithName(\"GetGarageSummary\")");

        // Must be query-side, not hardcoded.
        endpointBlock.ShouldContain("IQuerySession", Case.Sensitive);
        endpointBlock.ShouldContain("LoadAsync<GarageSummary>", Case.Sensitive);

        // Must ensure newest-first ordering.
        // We allow either projection-time ordering (Insert(0)) or endpoint-time ordering (OrderByDescending).
        var hasNewestFirstOrdering =
            endpointBlock.Contains("OrderByDescending", StringComparison.Ordinal) ||
            endpointBlock.Contains("Insert(0", StringComparison.Ordinal);

        hasNewestFirstOrdering.ShouldBeTrue(
            "GET /api/v1/garage/summary must return vehicles sorted by createdAt desc (newest first).");
    }

    [Fact]
    public void GetVehicleEndpoint_WhenNotFoundOrCrossUser_ShouldReturnProblemDetails404_NotBareNotFound()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text, "app.MapGet(\"/api/v1/garage/vehicles/{vehicleId:guid}\"", ".WithName(\"GetVehicle\")");

        // Contract: errors are ProblemDetails (application/problem+json).
        // Access rule: cross-user access should be 404 (not 403) to avoid leaking existence.
        // This test requires a 404 *ProblemDetails* response for both missing and cross-user vehicleIds.
        var returnsProblem404 =
            endpointBlock.Contains("Results.Problem", StringComparison.Ordinal) ||
            endpointBlock.Contains("TypedResults.Problem", StringComparison.Ordinal);

        returnsProblem404.ShouldBeTrue(
            "GetVehicle must return a 404 ProblemDetails (not an empty 404) when the vehicle does not exist for the authenticated user (including cross-user access).");
    }

    private static string ReadAllCsText(string dir)
    {
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(0, $"Expected at least one .cs file under '{dir}'.");

        var sb = new StringBuilder();
        foreach (var file in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(File.ReadAllText(file));
            sb.AppendLine();
        }

        return sb.ToString();
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
