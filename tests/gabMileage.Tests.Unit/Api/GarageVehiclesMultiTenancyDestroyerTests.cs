using Shouldly;

namespace gabMileage.Tests.Unit.Api;

public sealed class GarageVehiclesMultiTenancyDestroyerTests
{
    [Fact]
    public void GetVehicleEndpoint_MustScopeLookup_ToAuthenticatedUserId()
    {
        // Threat model: user A guesses user B's vehicleId and tries to fetch it.
        // Requirement: endpoint must filter by BOTH vehicleId and authContext.UserId, and return 404 if it doesn't match.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);

        // Guardrail: query must include a user-scope predicate.
        text.ShouldContain("Where(v => v.Id == vehicleId && v.UserId == authContext.UserId)", Case.Sensitive);
    }

    [Fact]
    public void UpdateVehicleHandler_MustReject_CrossUserUpdates()
    {
        // Threat model: user A calls PUT for user B's vehicleId.
        // Requirement: handler must reject by verifying read-model ownership before appending events.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Handlers", "UpdateVehicleHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Ownership check signal: compare existing.UserId to auth.UserId and throw not-found on mismatch.
        text.ShouldContain("existing.UserId", Case.Sensitive);
        text.ShouldContain("auth.UserId", Case.Sensitive);
        text.ShouldContain("VehicleNotFoundException", Case.Sensitive);

        var hasOwnershipGuard =
            text.Contains("!string.Equals(existing.UserId, auth.UserId", StringComparison.Ordinal) ||
            text.Contains("existing.UserId != auth.UserId", StringComparison.Ordinal);

        hasOwnershipGuard.ShouldBeTrue(
            "Expected UpdateVehicleHandler to verify existing.UserId matches auth.UserId before appending VehicleUpdated.");
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
