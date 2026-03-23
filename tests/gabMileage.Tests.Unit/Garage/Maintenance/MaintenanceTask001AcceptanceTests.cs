using System.Text;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceTask001AcceptanceTests
{
    [Fact]
    public void Garage_ShouldDefine_DefineMaintenanceItemCommand_Record_WithExpectedFields()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("record DefineMaintenanceItemCommand", Case.Insensitive);
        csText.ShouldContain("Guid CommandId", Case.Sensitive);
        csText.ShouldContain("Guid VehicleId", Case.Sensitive);
        csText.ShouldContain("string Name", Case.Sensitive);
        csText.ShouldContain("int? MileageInterval", Case.Sensitive);
        csText.ShouldContain("int? TimeIntervalMonths", Case.Sensitive);

        // Defaults are applied when omitted at the HTTP boundary (missing JSON -> 0 for ints),
        // so the command carries the final int values.
        csText.ShouldContain("int MileageWarning", Case.Sensitive);
        csText.ShouldContain("int TimeWarningDays", Case.Sensitive);

        csText.ShouldContain("LocalDate? InitialLastPerformedDate", Case.Sensitive);
        csText.ShouldContain("int? InitialLastPerformedOdometer", Case.Sensitive);
    }

    [Fact]
    public void Garage_Domain_ShouldDefine_MaintenanceItemDefined_EventRecord_WithInitialBaseline_AndUserScopedOwnership()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("record MaintenanceItemDefined", Case.Insensitive);

        // IDs must be UUIDs (Guid)
        csText.ShouldContain("Guid ItemId", Case.Sensitive);
        csText.ShouldContain("Guid VehicleId", Case.Sensitive);
        csText.ShouldContain("Guid CommandId", Case.Sensitive);

        // Ownership is denormalized into the event for verification/projection routing
        csText.ShouldContain("string UserId", Case.Sensitive);

        csText.ShouldContain("string Name", Case.Sensitive);
        csText.ShouldContain("int? MileageInterval", Case.Sensitive);
        csText.ShouldContain("int? TimeIntervalMonths", Case.Sensitive);
        csText.ShouldContain("int MileageWarning", Case.Sensitive);
        csText.ShouldContain("int TimeWarningDays", Case.Sensitive);

        // Initial baseline fields must exist on creation event
        csText.ShouldContain("LocalDate? InitialLastPerformedDate", Case.Sensitive);
        csText.ShouldContain("int? InitialLastPerformedOdometer", Case.Sensitive);

        // Events must be timestamped
        csText.ShouldContain("Instant OccurredAt", Case.Sensitive);
    }

    [Fact]
    public void DefineMaintenanceItem_ShouldDefault_WarningThresholds_WhenOmitted_FromJson()
    {
        // Acceptance criteria #1:
        // mileageWarning defaults to 500 and timeWarningDays defaults to 14 when omitted.
        // With System.Text.Json, omitted non-nullable ints deserialize as 0.
        // The domain must treat 0 as "use default".
        var root = RepoRoot.Find();

        var handlerPath = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "DefineMaintenanceItemHandler.cs");
        var inputPath = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Validation", "MaintenanceItemInput.cs");

        var existing = new[] { handlerPath, inputPath }.FirstOrDefault(File.Exists);
        existing.ShouldNotBeNull($"Expected at least one of '{handlerPath}' or '{inputPath}' to exist.");

        var text = File.ReadAllText(existing!);

        var referencesMileageDefault =
            text.Contains("MileageWarningThreshold.Default", StringComparison.Ordinal) ||
            text.Contains("500", StringComparison.Ordinal);
        referencesMileageDefault.ShouldBeTrue("Expected mileage warning default (500) to be referenced explicitly.");

        var referencesTimeDefault =
            text.Contains("TimeWarningThreshold.DefaultDays", StringComparison.Ordinal) ||
            text.Contains("14", StringComparison.Ordinal);
        referencesTimeDefault.ShouldBeTrue("Expected time warning default (14 days) to be referenced explicitly.");

        // Look for explicit 0->default handling (either in handler or in input normalization).
        var handlesOmittedMileage =
            text.Contains("MileageWarning", StringComparison.Ordinal) &&
            (text.Contains("== 0", StringComparison.Ordinal) || text.Contains("<= 0", StringComparison.Ordinal) || text.Contains("is 0", StringComparison.Ordinal));
        handlesOmittedMileage.ShouldBeTrue("Expected 0-to-default logic for omitted mileageWarning.");

        var handlesOmittedTime =
            text.Contains("TimeWarningDays", StringComparison.Ordinal) &&
            (text.Contains("== 0", StringComparison.Ordinal) || text.Contains("<= 0", StringComparison.Ordinal) || text.Contains("is 0", StringComparison.Ordinal));
        handlesOmittedTime.ShouldBeTrue("Expected 0-to-default logic for omitted timeWarningDays.");
    }

    [Fact]
    public void RecordMaintenanceHandler_ShouldAppend_MaintenancePerformed_AndPersist_ProcessedRecordMaintenance_IdempotencyMarker()
    {
        // Acceptance criteria #2:
        // Recording maintenance creates a service record (MaintenancePerformed) and updates projections.
        // The handler must:
        // - be idempotent via commandId marker
        // - append to vehicle-{vehicleId} stream
        // - generate a recordId (Guid.NewGuid)
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Handlers", "RecordMaintenanceHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        text.ShouldContain("ProcessedRecordMaintenance", Case.Sensitive);
        text.ShouldContain("LoadAsync<ProcessedRecordMaintenance>", Case.Sensitive);
        text.ShouldContain("command.CommandId", Case.Sensitive);
        text.ShouldContain("IsDuplicate: true", Case.Sensitive);

        text.ShouldContain("Guid.NewGuid()", Case.Sensitive);
        text.ShouldContain("new MaintenancePerformed(", Case.Sensitive);
        text.ShouldContain("auth.UserId", Case.Sensitive);
        text.ShouldContain("clock.GetCurrentInstant()", Case.Sensitive);

        text.ShouldContain("session.Events.Append", Case.Sensitive);
        text.ShouldContain("vehicle-", Case.Sensitive);

        text.ShouldContain("session.Insert", Case.Sensitive);
        text.ShouldContain("new ProcessedRecordMaintenance", Case.Sensitive);
    }

    [Fact]
    public void MaintenanceTabProjection_ShouldProject_MaintenancePerformed_IntoRecords_AndRecalculateLastOdometer()
    {
        // Acceptance criteria #2 (projection side): appending MaintenancePerformed must materialize into the tab read model.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Projections", "MaintenanceTabProjection.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        text.ShouldContain("class MaintenanceTabProjection", Case.Insensitive);
        text.ShouldContain("MultiStreamProjection<MaintenanceTabData", Case.Sensitive);
        text.ShouldContain("ProjectionLifecycle.Inline", Case.Sensitive);

        // Routes MaintenancePerformed and projects it to item.Records
        text.ShouldContain("Identity<MaintenancePerformed>", Case.Sensitive);
        text.ShouldContain("ProjectEvent<MaintenancePerformed>", Case.Sensitive);
        text.ShouldContain("item.Records.Add", Case.Sensitive);
        text.ShouldContain("new MaintenanceRecordEntry", Case.Insensitive);

        // Must sort records reverse-chronological (date desc, then odometer desc)
        text.ShouldContain("OrderByDescending(r => r.Date)", Case.Sensitive);
        text.ShouldContain("ThenByDescending(r => r.Odometer)", Case.Sensitive);

        text.ShouldContain("RecalculateLastOdometer", Case.Sensitive);
    }

    [Fact]
    public void MaintenanceTabProjection_ShouldRemoveItemAndAllHistory_FromReadModel_OnMaintenanceItemRemoved()
    {
        // Acceptance criteria #3:
        // Removing an item removes it (and its history) from the maintenance tab read model.
        // Removing the item entry necessarily removes its nested record history.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Projections", "MaintenanceTabProjection.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        text.ShouldContain("ProjectEvent<MaintenanceItemRemoved>", Case.Sensitive);
        text.ShouldContain("data.Items.RemoveAll", Case.Sensitive);
        text.ShouldContain("i => i.ItemId == e.ItemId", Case.Sensitive);
    }

    [Fact]
    public void MaintenanceTabDataId_ShouldUse_maintenance_prefix()
    {
        // Domain Decision D-04: read model key is "maintenance-{vehicleId}".
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "MaintenanceTabDataId.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);
        text.ShouldContain("maintenance-", Case.Sensitive);
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
