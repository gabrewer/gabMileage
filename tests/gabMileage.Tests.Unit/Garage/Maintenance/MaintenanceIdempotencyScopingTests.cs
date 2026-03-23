using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceIdempotencyScopingTests
{
    [Fact]
    public void ProcessedDefineMaintenanceItem_ShouldRecord_VehicleId_ForMismatchDetection()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "ReadModels", "ProcessedDefineMaintenanceItem.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);
        text.ShouldContain("Guid VehicleId", Case.Sensitive);
    }

    [Fact]
    public void DefineMaintenanceItemHandler_ShouldCheck_OwnershipBeforeIdempotency()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "DefineMaintenanceItemHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var ownershipIndex = text.IndexOf("// Ownership check (vehicle)", StringComparison.Ordinal);
        var idempotencyIndex = text.IndexOf("// Idempotency check", StringComparison.Ordinal);

        ownershipIndex.ShouldBeGreaterThanOrEqualTo(0);
        idempotencyIndex.ShouldBeGreaterThanOrEqualTo(0);
        ownershipIndex.ShouldBeLessThan(idempotencyIndex);

        text.ShouldContain("processed.VehicleId", Case.Sensitive);
    }

    [Fact]
    public void ProcessedRecordMaintenance_ShouldRecord_VehicleIdAndItemId_ForMismatchDetection()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "ReadModels", "ProcessedRecordMaintenance.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);
        text.ShouldContain("Guid VehicleId", Case.Sensitive);
        text.ShouldContain("Guid ItemId", Case.Sensitive);
    }

    [Fact]
    public void RecordMaintenanceHandler_ShouldCheck_OwnershipBeforeIdempotency()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "RecordMaintenanceHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var ownershipIndex = text.IndexOf("// Ownership check (vehicle)", StringComparison.Ordinal);
        var idempotencyIndex = text.IndexOf("// Idempotency check", StringComparison.Ordinal);

        ownershipIndex.ShouldBeGreaterThanOrEqualTo(0);
        idempotencyIndex.ShouldBeGreaterThanOrEqualTo(0);
        ownershipIndex.ShouldBeLessThan(idempotencyIndex);

        text.ShouldContain("processed.VehicleId", Case.Sensitive);
        text.ShouldContain("processed.ItemId", Case.Sensitive);
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
