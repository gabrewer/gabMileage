using System.Text;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceTask004AcceptanceTests
{
    private static readonly string[] ExpectedWriteRoutes =
    [
        "/api/v1/garage/vehicles/{vehicleId}/maintenance/items",
        "/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}",
        "/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records",
        "/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}"
    ];

    [Fact]
    public void Api_ShouldExpose_MaintenanceWriteEndpoints_ForItemsAndRecords()
    {
        // TASK-004 acceptance criteria #1 (write surface exists):
        // Frontend needs endpoints to define/update/remove items and record/update/delete records.
        var root = RepoRoot.Find();
        var programPath = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programPath).ShouldBeTrue($"Expected '{programPath}' to exist.");

        var text = File.ReadAllText(programPath);

        // Ensure all canonical routes exist. (Specific MapPost/MapPut/MapDelete shapes are checked in the next test.)
        foreach (var route in ExpectedWriteRoutes)
            text.ShouldContain(route, Case.Sensitive);

        // Ensure we map the correct verbs for each route.
        ExtractFollowingText(text, "\"/api/v1/garage/vehicles/{vehicleId}/maintenance/items\"")
            .ShouldContain("MapPost", Case.Insensitive);

        ExtractFollowingText(text, "\"/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}\"")
            .ShouldContain("MapPut", Case.Insensitive);

        ExtractFollowingText(text, "\"/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}\"")
            .ShouldContain("MapDelete", Case.Insensitive);

        ExtractFollowingText(text, "\"/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records\"")
            .ShouldContain("MapPost", Case.Insensitive);

        ExtractFollowingText(text, "\"/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}\"")
            .ShouldContain("MapPut", Case.Insensitive);

        ExtractFollowingText(text, "\"/api/v1/garage/vehicles/{vehicleId}/maintenance/items/{itemId}/records/{recordId}\"")
            .ShouldContain("MapDelete", Case.Insensitive);
    }

    [Fact]
    public void MaintenanceWriteEndpoints_ShouldReturn_404_ForCrossUserAccess()
    {
        // TASK-004 acceptance criteria #3:
        // Any cross-user access must behave as 404 (never 403).
        var root = RepoRoot.Find();
        var programPath = Path.Combine(root, "gabMileage.Api", "Program.cs");
        var text = File.ReadAllText(programPath);

        foreach (var route in ExpectedWriteRoutes)
        {
            var marker = $"\"{route}\"";
            var block = ExtractFollowingText(text, marker, maxChars: 9000);

            // Enforce 404 mapping (most handlers throw VehicleNotFoundException for ownership failures).
            block.ShouldContain("catch (VehicleNotFoundException", Case.Insensitive);
            block.ShouldContain("StatusCodes.Status404NotFound", Case.Sensitive);

            // Ensure we never use 403 on these endpoints.
            block.ShouldNotContain("StatusCodes.Status403Forbidden", Case.Sensitive);
            block.ShouldNotContain("Forbidden", Case.Insensitive);
        }
    }

    [Fact]
    public void MaintenanceWriteEndpoints_ShouldReturn_400_WithBriefValidationMessages()
    {
        // TASK-004 acceptance criteria #2:
        // Validation errors must match the Sprint 04 brief (exact message strings).
        // Pattern: handlers throw a *ValidationException with an errors dictionary;
        // endpoints catch and return Results.ValidationProblem(errors).
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Require a dedicated maintenance validation exception type like FuelLogValidationException.
        csText.ShouldContain("class MaintenanceValidationException", Case.Insensitive);
        csText.ShouldContain("Results.ValidationProblem", Case.Sensitive);

        // Maintenance item definition validation messages
        csText.ShouldContain("Give this item a name", Case.Sensitive);
        csText.ShouldContain("Set a mileage interval, time interval, or both", Case.Sensitive);
        csText.ShouldContain("Enter a valid mileage interval", Case.Sensitive);
        csText.ShouldContain("Enter a valid time interval (1–120 months)", Case.Sensitive);
        csText.ShouldContain("Warning threshold must be less than the interval", Case.Sensitive);

        // Shared date validation message (must apply to initial baseline and records)
        csText.ShouldContain("Date can't be in the future", Case.Sensitive);
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

    private static string ExtractFollowingText(string text, string marker, int maxChars = 12000)
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
