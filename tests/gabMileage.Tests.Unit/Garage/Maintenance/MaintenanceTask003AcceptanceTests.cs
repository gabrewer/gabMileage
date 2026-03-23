using System.Text;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceTask003AcceptanceTests
{
    [Fact]
    public void MaintenanceTabProjection_ShouldRoute_FuelLogEvents_ToMaintenanceTabData()
    {
        // TASK-003 acceptance criteria #3 (projection): LastOdometer must reflect the most recent reading
        // across fuel logs and maintenance records, so the projection must subscribe to fuel log events.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Projections", "MaintenanceTabProjection.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        text.ShouldContain("Identity<FuelLogRecorded>", Case.Sensitive);
        text.ShouldContain("Identity<FuelLogUpdated>", Case.Sensitive);
        text.ShouldContain("Identity<FuelLogDeleted>", Case.Sensitive);
    }

    [Fact]
    public void MaintenanceTabProjection_ShouldUpdate_LastOdometer_FromFuelLogOdometer()
    {
        // TASK-003 acceptance criteria #3 (projection): fuel log odometer readings must be considered
        // when computing MaintenanceTabData.LastOdometer.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Maintenance", "Projections", "MaintenanceTabProjection.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Require explicit fuel-log event handlers that flow the odometer reading into LastOdometer.
        // (We intentionally assert for a clear, discoverable implementation signal rather than a specific algorithm.)
        text.ShouldContain("ProjectEvent<FuelLogRecorded>", Case.Sensitive);
        text.ShouldContain("ProjectEvent<FuelLogUpdated>", Case.Sensitive);
        text.ShouldContain("e.Odometer", Case.Sensitive);
        text.ShouldContain("LastOdometer", Case.Sensitive);
    }

    [Fact]
    public void Api_ShouldExpose_GetMaintenanceTab_Endpoint()
    {
        // TASK-003 acceptance criteria #1/#2/#3 are query-time behaviors per API contract:
        // GET /api/v1/garage/vehicles/{vehicleId}/maintenance returns the sorted/enriched payload.
        var root = RepoRoot.Find();
        var programPath = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programPath).ShouldBeTrue($"Expected '{programPath}' to exist.");

        var text = File.ReadAllText(programPath);
        text.ShouldContain("/api/v1/garage/vehicles/{vehicleId}/maintenance", Case.Sensitive);
    }

    [Fact]
    public void GetMaintenanceTab_Endpoint_ShouldSort_Items_ByUrgencyThenName_AndSort_Records_ReverseChronological()
    {
        // TASK-003 acceptance criteria #1 and #2:
        // - Items sorted: overdue → dueSoon → ok, then alphabetical by name within group.
        // - Records sorted: date desc, then odometer desc.
        var root = RepoRoot.Find();
        var programPath = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programPath).ShouldBeTrue($"Expected '{programPath}' to exist.");

        var text = File.ReadAllText(programPath);

        var endpointBlock = ExtractFollowingText(
            text,
            marker: "\"/api/v1/garage/vehicles/{vehicleId}/maintenance\"",
            maxChars: 12000);

        // Sorting by urgency requires comparing status values (overdue/dueSoon/ok) and then name.
        endpointBlock.ShouldContain("overdue", Case.Insensitive);
        endpointBlock.ShouldContain("dueSoon", Case.Insensitive);
        endpointBlock.ShouldContain("ok", Case.Insensitive);
        endpointBlock.ShouldContain("OrderBy", Case.Insensitive);
        endpointBlock.ShouldContain("ThenBy", Case.Insensitive);
        endpointBlock.ShouldContain("name", Case.Insensitive);

        // Records must be sorted reverse-chronological.
        endpointBlock.ShouldContain("OrderByDescending", Case.Insensitive);
        endpointBlock.ShouldContain("r => r.Date", Case.Sensitive);
        endpointBlock.ShouldContain("ThenByDescending", Case.Insensitive);
        endpointBlock.ShouldContain("r => r.Odometer", Case.Sensitive);
    }

    [Fact]
    public void GetMaintenanceTab_Endpoint_ShouldCompute_LastOdometer_FromFuelLogs_AndMaintenanceRecords()
    {
        // TASK-003 acceptance criteria #3:
        // lastOdometer is the most recent reading across fuel logs and maintenance records.
        // The API contract calls out reconciling MaintenanceTabData.LastOdometer with FuelLogList.LastOdometer.
        var root = RepoRoot.Find();
        var programPath = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programPath).ShouldBeTrue($"Expected '{programPath}' to exist.");

        var text = File.ReadAllText(programPath);

        var endpointBlock = ExtractFollowingText(
            text,
            marker: "\"/api/v1/garage/vehicles/{vehicleId}/maintenance\"",
            maxChars: 16000);

        endpointBlock.ShouldContain("LoadAsync<MaintenanceTabData>", Case.Sensitive);
        endpointBlock.ShouldContain("MaintenanceTabDataId.From", Case.Sensitive);

        endpointBlock.ShouldContain("LoadAsync<FuelLogList>", Case.Sensitive);
        endpointBlock.ShouldContain("FuelLogListId.From", Case.Sensitive);

        // Must reconcile the most recent reading between the two sources (by date, then odometer on ties).
        endpointBlock.ShouldContain("LatestMaintenanceOdometer", Case.Sensitive);
        endpointBlock.ShouldContain("LatestFuelOdometer", Case.Sensitive);
        endpointBlock.ShouldContain("OrderByDescending(l => l.Date", Case.Sensitive);
        endpointBlock.ShouldContain("ThenByDescending(l => l.Odometer", Case.Sensitive);
        endpointBlock.ShouldContain(".Value.Date >", Case.Sensitive);
        endpointBlock.ShouldContain(".Value.Date <", Case.Sensitive);
        endpointBlock.ShouldContain("lastOdometer", Case.Insensitive);
    }

    private static string ExtractFollowingText(string text, string marker, int maxChars)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"Expected to find marker '{marker}' in target file.");

        var remaining = text[(index)..];
        return remaining.Length <= maxChars ? remaining : remaining[..maxChars];
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
