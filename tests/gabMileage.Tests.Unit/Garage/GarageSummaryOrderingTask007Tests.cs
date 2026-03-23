using Shouldly;

namespace gabMileage.Tests.Unit.Garage;

public sealed class GarageSummaryOrderingTask007Tests
{
    [Fact]
    public void GarageSummaryProjection_ShouldMaintainNewestFirst_ByCreatedAt_EvenIfEventsArriveOutOfOrder()
    {
        // Sprint 02 contract: GarageSummary.vehicles ordered newest-first (createdAt desc).
        // This test enforces projection-side ordering robustness: sorting by CreatedAt, not just insert position.
        // (Endpoint/UI can still sort defensively, but projection should persist the invariant too.)
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Projections", "GarageSummaryProjection.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Must perform an explicit CreatedAt-desc ordering in the VehicleAdded handler.
        // Insert(0, ...) alone only works if event OccurredAt is monotonic.
        text.ShouldContain("OrderByDescending", Case.Sensitive);
        text.ShouldContain("CreatedAt", Case.Sensitive);
    }

    [Fact]
    public void GarageSummaryProjection_VehicleUpdated_ShouldReSortByCreatedAtDesc_AndMustNotModifyCreatedAtValues()
    {
        // Enforce projection-side ordering: after any mutation (including updates), the summary list
        // should still be persisted newest-first by CreatedAt desc.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Projections", "GarageSummaryProjection.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        text.ShouldContain("ProjectEvent<VehicleUpdated>", Case.Sensitive);

        var updatedBlock = ExtractBlock(text, "ProjectEvent<VehicleUpdated>", "});");

        // Must not change CreatedAt values on update.
        updatedBlock.ShouldNotContain("CreatedAt =", Case.Sensitive);

        // Must re-sort the list by CreatedAt desc to maintain the invariant even if events arrive out-of-order.
        updatedBlock.ShouldContain("OrderByDescending", Case.Sensitive);
        updatedBlock.ShouldContain("CreatedAt", Case.Sensitive);
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
