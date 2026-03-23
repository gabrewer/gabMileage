using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceTask001DestroyerTests
{
    [Fact]
    public void DefineMaintenanceItem_MustRequireAtLeastOneInterval_MilesOrMonths()
    {
        // Business rule: a maintenance item is meaningless without at least one interval (miles and/or months).
        // Guardrail: handler or validation layer must explicitly reject (MileageInterval == null && TimeIntervalMonths == null)
        // or non-positive interval values.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "DefineMaintenanceItemHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Guardrail signal: explicit validation branching on interval fields (not warning thresholds).
        var hasMileageIntervalValidation =
            text.Contains("if (command.MileageInterval", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval is null", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval == null", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval <= 0", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval < 1", StringComparison.Ordinal);

        var hasTimeIntervalValidation =
            text.Contains("if (command.TimeIntervalMonths", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths is null", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths == null", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths <= 0", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths < 1", StringComparison.Ordinal);

        (hasMileageIntervalValidation || hasTimeIntervalValidation).ShouldBeTrue(
            "DefineMaintenanceItem must validate that at least one interval (miles/months) is present and positive. Currently it appears possible to create an item with no interval, making it never due.");
    }

    [Fact]
    public void UpdateMaintenanceItem_MustRequireAtLeastOneInterval_MilesOrMonths()
    {
        // Same invariant should hold for updates, otherwise an update can silently erase all intervals.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "UpdateMaintenanceItemHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var hasMileageIntervalValidation =
            text.Contains("if (command.MileageInterval", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval is null", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval == null", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval <= 0", StringComparison.Ordinal)
            || text.Contains("command.MileageInterval < 1", StringComparison.Ordinal);

        var hasTimeIntervalValidation =
            text.Contains("if (command.TimeIntervalMonths", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths is null", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths == null", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths <= 0", StringComparison.Ordinal)
            || text.Contains("command.TimeIntervalMonths < 1", StringComparison.Ordinal);

        (hasMileageIntervalValidation || hasTimeIntervalValidation).ShouldBeTrue(
            "UpdateMaintenanceItem must validate that at least one interval (miles/months) remains present and positive. Otherwise an update can erase intervals and make the item never due.");
    }

    [Fact]
    public void RecordMaintenance_MustValidate_OdometerIsPositive()
    {
        // Domain rule: odometer must be positive.
        // Guardrail: handler must reject 0/negative odometer before any DB work.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "RecordMaintenanceHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var hasOdometerGuard =
            (text.Contains("if (command.Odometer <= 0)", StringComparison.Ordinal)
             || text.Contains("if (command.Odometer < 1)", StringComparison.Ordinal)
             || text.Contains("if (command.Odometer == 0)", StringComparison.Ordinal))
            && text.Contains("throw", StringComparison.Ordinal);

        hasOdometerGuard.ShouldBeTrue(
            "RecordMaintenance must validate odometer > 0. Without this, projections can set LastOdometer to 0 and break due-calculations and UI ordering.");
    }

    [Fact]
    public void UpdateMaintenanceRecord_MustValidate_OdometerIsPositive()
    {
        // Same invariant should hold for updating an existing record.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "UpdateMaintenanceRecordHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var hasOdometerGuard =
            (text.Contains("if (command.Odometer <= 0)", StringComparison.Ordinal)
             || text.Contains("if (command.Odometer < 1)", StringComparison.Ordinal)
             || text.Contains("if (command.Odometer == 0)", StringComparison.Ordinal))
            && text.Contains("throw", StringComparison.Ordinal);

        hasOdometerGuard.ShouldBeTrue(
            "UpdateMaintenanceRecord must validate odometer > 0. Otherwise records can be edited into invalid states and re-order the 'last performed' baseline incorrectly.");
    }

    [Fact]
    public void RecordMaintenance_ShouldValidate_ItemIdIsNotEmptyGuid()
    {
        // Threat model: a client sends ItemId = Guid.Empty; current code will do a projection lookup and then throw a generic not-found.
        // Guardrail: treat this as a bad command and reject early.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "RecordMaintenanceHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var hasItemIdGuard =
            text.Contains("if (command.ItemId == Guid.Empty)", StringComparison.Ordinal)
            || text.Contains("if (command.ItemId == Guid.Empty)", StringComparison.Ordinal);

        hasItemIdGuard.ShouldBeTrue(
            "RecordMaintenance must validate ItemId != Guid.Empty to avoid ambiguous not-found behavior and reduce attack surface for malformed commands.");
    }

    [Fact]
    public void ProcessedRecordMaintenanceId_IsScopedOnlyToUserAndCommand_NotVehicle()
    {
        // Adversarial: if a client accidentally reuses a commandId across two vehicles,
        // this id format will treat the second request as a duplicate and return the first RecordId.
        // This test documents the current behavior/risk.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "ProcessedRecordMaintenanceId.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);
        text.ShouldContain("$\"{userId}::{commandId:D}\"", Case.Sensitive);
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
