using Shouldly;

namespace gabMileage.Tests.Unit.Garage;

public sealed class GarageSummaryProjectionDestroyerTests
{
    [Fact]
    public void GetVehicleEndpoint_MustScopeReadModelLookup_ByAuthenticatedUserId()
    {
        // Threat model:
        // - If the query loads by vehicleId/streamId only, an attacker can probe existence of other users' vehicles.
        // - Worse: if the response differs (403 vs 404), it leaks that the vehicle exists.
        // Guardrail:
        // - Query must include v.UserId == authContext.UserId in the DB predicate.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text,
            "app.MapGet(\"/api/v1/garage/vehicles/{vehicleId:guid}\"",
            ".WithName(\"GetVehicle\")");

        endpointBlock.ShouldContain("Query<VehicleReadModel>", Case.Sensitive);

        var hasUserScopedPredicate =
            endpointBlock.Contains("v.UserId == authContext.UserId", StringComparison.Ordinal) ||
            endpointBlock.Contains("v.UserId==authContext.UserId", StringComparison.Ordinal);

        hasUserScopedPredicate.ShouldBeTrue(
            "GetVehicle must scope reads by authenticated user in the query predicate (v.UserId == authContext.UserId). Without this, cross-user vehicleIds can leak existence.");
    }

    [Fact]
    public void GetVehicleEndpoint_CrossUserAccess_MustNotReturn403()
    {
        // Threat model:
        // - 403 vs 404 behavior differences leak whether another user's vehicle exists.
        // Guardrail:
        // - Cross-user access must behave as NotFound (404), not Forbidden (403).
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text,
            "app.MapGet(\"/api/v1/garage/vehicles/{vehicleId:guid}\"",
            ".WithName(\"GetVehicle\")");

        endpointBlock.ShouldNotContain("StatusCodes.Status403Forbidden", Case.Sensitive);
        endpointBlock.ShouldNotContain("Results.Forbid", Case.Sensitive);
        endpointBlock.ShouldNotContain("Forbidden", Case.Insensitive);

        // Positive indicator that the not-found path is explicit.
        var has404 = endpointBlock.Contains("StatusCodes.Status404NotFound", StringComparison.Ordinal) ||
                    endpointBlock.Contains("Results.NotFound", StringComparison.Ordinal);

        has404.ShouldBeTrue(
            "GetVehicle should return 404 for missing/cross-user vehicleIds (avoid 403 existence leaks). Expected an explicit 404 path.");
    }

    [Fact]
    public void GetGarageSummaryEndpoint_MustReturnVehicles_SortedByCreatedAtDesc()
    {
        // Threat model:
        // - If sorting is ascending or absent, UI renders oldest-first, violating contract.
        // Guardrail:
        // - Must apply OrderByDescending(v => v.CreatedAt) either in projection or endpoint.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        var text = File.ReadAllText(programCs);
        var endpointBlock = ExtractBlock(text,
            "app.MapGet(\"/api/v1/garage/summary\"",
            ".WithName(\"GetGarageSummary\")");

        endpointBlock.ShouldContain("OrderByDescending", Case.Sensitive,
            "GET /api/v1/garage/summary must enforce newest-first ordering (createdAt desc) to avoid relying on projection list insertion order.");

        // Catch the common bug: OrderBy(v => v.CreatedAt) (ascending) or sorting by UpdatedAt.
        endpointBlock.ShouldNotContain("OrderBy(v => v.CreatedAt)", Case.Sensitive);
        endpointBlock.ShouldNotContain("OrderBy(v=>v.CreatedAt)", Case.Sensitive);
    }

    [Fact]
    public void GarageSummaryProjection_MustRouteByUserId_AndInsertNewestFirst()
    {
        // Threat model:
        // - If projection routes by streamId or vehicleId, a user could receive another user's summary.
        // - If new vehicles are appended, ordering becomes oldest-first.
        var root = RepoRoot.Find();
        var projectionPath = Path.Combine(root, "gabMileage.Api", "Garage", "Projections", "GarageSummaryProjection.cs");
        File.Exists(projectionPath).ShouldBeTrue($"Expected '{projectionPath}' to exist.");

        var text = File.ReadAllText(projectionPath);

        text.ShouldContain("MultiStreamProjection<GarageSummary", Case.Sensitive);
        text.ShouldContain("Identity<VehicleAdded>(e => e.UserId)", Case.Sensitive);
        text.ShouldContain("Identity<VehicleUpdated>(e => e.UserId)", Case.Sensitive);

        // Newest-first insertion should be explicit (even though endpoint should also sort defensively).
        text.ShouldContain("vehicles.Insert(0", Case.Sensitive);
        text.ShouldContain("CreatedAt = e.OccurredAt", Case.Sensitive);

        // Ensure we're not also doing an accidental oldest-first append in the add handler.
        var addedBlock = ExtractBlock(text, "ProjectEvent<VehicleAdded>", "});");
        addedBlock.ShouldNotContain("vehicles.Add(", Case.Sensitive);
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
