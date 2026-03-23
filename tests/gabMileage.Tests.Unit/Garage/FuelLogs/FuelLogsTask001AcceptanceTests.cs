using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class FuelLogsTask001AcceptanceTests
{
    [Fact]
    public void RecordFuelLogCommand_WithFewerThanTwoFuelFields_ShouldRejectWithBriefMessage()
    {
        // Sprint 03 brief: Error if fewer than two of {gallons, totalCost, pricePerGallon}:
        // "Enter at least two fuel fields — we'll calculate the third."
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "FuelLogs", "Validation", "FuelFieldCalculator.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);
        text.ShouldContain("Enter at least two fuel fields — we'll calculate the third.", Case.Sensitive);
    }

    [Fact]
    public void RecordFuelLogCommand_WithFutureDate_ShouldRejectWithExactMessage()
    {
        // Acceptance criteria: Future dates are rejected with "Date can't be in the future".
        // Domain model sketch: FuelLogInput validates date vs today and sets errors["date"].
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "FuelLogs", "Validation", "FuelLogInput.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);
        text.ShouldContain("Date can't be in the future", Case.Sensitive);
        text.ShouldContain("errors[\"date\"]", Case.Sensitive);
    }

    [Fact]
    public void RecordFuelLogCommand_DuplicateCommandId_ShouldBeIdempotentViaProcessedRecordFuelLogMarker()
    {
        // Acceptance criteria: Duplicate RecordFuelLogCommand with same commandId is idempotent.
        // Domain model: handler loads ProcessedRecordFuelLog keyed by (userId, commandId)
        // and returns the previously created FuelLogId with IsDuplicate=true (no event emitted).
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "FuelLogs", "Handlers", "RecordFuelLogHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);
        text.ShouldContain("ProcessedRecordFuelLog", Case.Sensitive);
        text.ShouldContain("LoadAsync<ProcessedRecordFuelLog>", Case.Sensitive);
        text.ShouldContain("command.CommandId", Case.Sensitive);

        // Mirror the Sprint 02 pattern (AddVehicleHandler): return a result with IsDuplicate=true.
        text.ShouldContain("IsDuplicate: true", Case.Sensitive);
        text.ShouldContain("processed.FuelLogId", Case.Sensitive);
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
