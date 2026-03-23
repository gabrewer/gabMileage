using Shouldly;

namespace gabMileage.Tests.Unit.Garage;

public sealed class VehicleSliceDestroyerTests
{
    [Fact]
    public void AddVehicle_IdempotencyMarker_MustBeValidatedAgainstAuthenticatedUser()
    {
        // Threat model:
        // - If ProcessedAddVehicle is keyed only by commandId, a cross-user collision (or malicious replay)
        //   can cause user B to receive user A's vehicleId (multi-tenant data leak).
        // Guardrail:
        // - When a marker is found, handler must verify marker.UserId == auth.UserId before treating it as idempotent success.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Handlers", "AddVehicleHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var snippet = ExtractBlock(text,
            "var processed = await session.LoadAsync<ProcessedAddVehicle>",
            "var vehicleId = Guid.NewGuid()");

        snippet.ShouldContain("processed", Case.Insensitive);
        snippet.ShouldContain("return", Case.Insensitive);

        // The critical check we require:
        snippet.ShouldContain("processed.UserId", Case.Sensitive,
            "Idempotency must be scoped to the authenticated user. If a ProcessedAddVehicle is found, the handler must confirm marker.UserId == auth.UserId before returning marker.VehicleId.");
    }

    [Fact]
    public void UpdateVehicle_MustEnforceOwnership_BeforeAppendingEvents()
    {
        // Threat model:
        // - Without an ownership check, user A can update (or even hijack) user B's vehicle stream
        //   by sending B's vehicleId.
        // Guardrail:
        // - Handler must load an existing representation (read model or aggregate) and verify UserId matches auth.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Handlers", "UpdateVehicleHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var hasOwnershipRead =
            text.Contains("LoadAsync<VehicleReadModel>", StringComparison.Ordinal) ||
            text.Contains("Query<VehicleReadModel>", StringComparison.Ordinal) ||
            text.Contains("UserId != auth.UserId", StringComparison.Ordinal) ||
            text.Contains("UserId == auth.UserId", StringComparison.Ordinal);

        hasOwnershipRead.ShouldBeTrue(
            "UpdateVehicle must verify the vehicle exists and belongs to the authenticated user before appending VehicleUpdated. Otherwise cross-user updates and ownership hijack are possible.");
    }

    [Fact]
    public void VehicleAggregate_OwnershipMustBeImmutable_AcrossUpdates()
    {
        // Threat model:
        // - If Vehicle.Apply(VehicleUpdated) overwrites UserId, then any code path that emits an update
        //   with the caller's auth.UserId can effectively transfer ownership.
        // Guardrail:
        // - VehicleUpdated must not change ownership; UserId should be set only by VehicleAdded.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Vehicle.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var updatedApplyBlock = ExtractBraceBlock(text, "Apply(VehicleUpdated");
        updatedApplyBlock.ShouldNotContain("UserId =", Case.Sensitive,
            "Vehicle.Apply(VehicleUpdated) must not modify UserId; ownership must be immutable.");
    }

    [Fact]
    public void VehicleProjection_MustNotOverwrite_UserId_OnVehicleUpdated()
    {
        // Threat model:
        // - If the read model's UserId is overwritten on update, then a cross-user update can steal the vehicle
        //   in the query model (the attacker becomes the apparent owner).
        // Guardrail:
        // - The projection must keep UserId from VehicleAdded; update events may carry UserId for routing,
        //   but should not mutate the document owner.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Projections", "VehicleProjection.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var updatedMapping = ExtractBlock(text, "ProjectEvent<VehicleUpdated>", ");");
        updatedMapping.ShouldNotContain("rm.UserId", Case.Sensitive,
            "VehicleProjection must not change rm.UserId on updates; ownership must remain from VehicleAdded.");
    }

    [Fact]
    public void Api_ShouldDefine_VehicleEndpoints_UnderGaragePath()
    {
        // Acceptance criteria implies the vertical slice is wired end-to-end.
        // If no endpoints are mapped, commands/events/projections exist but are unreachable.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        text.ShouldContain("/api/v1/garage/vehicles", Case.Sensitive,
            "Expected vehicle endpoints to be mapped under /api/v1/garage/vehicles (POST/GET/PUT) per Sprint 02 contract.");
    }

    private static string ExtractBlock(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Could not find marker '{startMarker}'.");

        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, $"Could not find end marker '{endMarker}' after '{startMarker}'.");

        return text.Substring(start, end - start + endMarker.Length);
    }

    private static string ExtractBraceBlock(string text, string marker)
    {
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Could not find marker '{marker}'.");

        var open = text.IndexOf('{', start);
        open.ShouldBeGreaterThanOrEqualTo(0, "Expected '{' after marker.");

        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text.Substring(open, i - open + 1);
            }
        }

        throw new InvalidOperationException("Unbalanced braces while extracting block.");
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
