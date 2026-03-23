using System.Text;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage;

public sealed class VehiclesVerticalSliceTests
{
    [Fact]
    public void Garage_ShouldDefine_AddVehicleCommand_Record_WithExpectedFields()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Contract from docs/api + docs/domain
        csText.ShouldContain("record AddVehicleCommand", Case.Insensitive);
        csText.ShouldContain("Guid CommandId", Case.Sensitive);
        csText.ShouldContain("string Name", Case.Sensitive);
        csText.ShouldContain("string? Make", Case.Sensitive);
        csText.ShouldContain("string? Model", Case.Sensitive);
        csText.ShouldContain("int? Year", Case.Sensitive);
    }

    [Fact]
    public void Garage_ShouldDefine_UpdateVehicleCommand_Record_WithExpectedFields()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Contract from docs/api + docs/domain
        csText.ShouldContain("record UpdateVehicleCommand", Case.Insensitive);
        csText.ShouldContain("Guid VehicleId", Case.Sensitive);
        csText.ShouldContain("string Name", Case.Sensitive);
        csText.ShouldContain("string? Make", Case.Sensitive);
        csText.ShouldContain("string? Model", Case.Sensitive);
        csText.ShouldContain("int? Year", Case.Sensitive);
    }

    [Fact]
    public void Garage_Domain_ShouldDefine_VehicleAdded_EventRecord_WithUserId_AndCommandId()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("record VehicleAdded", Case.Insensitive);

        // Field contract from docs/api + docs/domain
        csText.ShouldContain("Guid VehicleId", Case.Sensitive);
        csText.ShouldContain("string UserId", Case.Sensitive);
        csText.ShouldContain("Guid CommandId", Case.Sensitive);
        csText.ShouldContain("string Name", Case.Sensitive);
        csText.ShouldContain("string? Make", Case.Sensitive);
        csText.ShouldContain("string? Model", Case.Sensitive);
        csText.ShouldContain("int? Year", Case.Sensitive);
        csText.ShouldContain("Instant OccurredAt", Case.Sensitive);
    }

    [Fact]
    public void Garage_Domain_ShouldDefine_VehicleUpdated_EventRecord_WithUserId_ForProjectionRouting()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("record VehicleUpdated", Case.Insensitive);

        // Per docs/domain Decision D-07, VehicleUpdated includes UserId (denormalized) for GarageSummary projection routing.
        csText.ShouldContain("Guid VehicleId", Case.Sensitive);
        csText.ShouldContain("string UserId", Case.Sensitive);
        csText.ShouldContain("string Name", Case.Sensitive);
        csText.ShouldContain("string? Make", Case.Sensitive);
        csText.ShouldContain("string? Model", Case.Sensitive);
        csText.ShouldContain("int? Year", Case.Sensitive);
        csText.ShouldContain("Instant OccurredAt", Case.Sensitive);
    }

    [Fact]
    public void Api_MartenConfiguration_ShouldRegister_VehicleEvents_And_InlineProjections()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var martenBlock = ExtractBlock(text, "AddMarten(options =>", "})");

        // Acceptance criteria: events must be persisted and read model must reflect updates immediately.
        // That requires event registration + inline projections.
        martenBlock.ShouldContain("AddEventType<VehicleAdded>", Case.Insensitive);
        martenBlock.ShouldContain("AddEventType<VehicleUpdated>", Case.Insensitive);

        martenBlock.ShouldContain("VehicleProjection", Case.Sensitive);
        martenBlock.ShouldContain("ProjectionLifecycle.Inline", Case.Sensitive);
    }

    [Fact]
    public void AddVehicleHandler_ShouldBeIdempotent_ByCommandId_UsingProcessedAddVehicle_Document()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Idempotency requirement: handler must check and store a commandId->vehicleId mapping.
        csText.ShouldContain("ProcessedAddVehicle", Case.Sensitive);

        // Detect duplicate submissions: Load by command id
        csText.ShouldContain("LoadAsync<ProcessedAddVehicle>", Case.Sensitive);
        csText.ShouldContain("command.CommandId", Case.Sensitive);

        // Persist idempotency marker in same transaction
        csText.ShouldContain("session.Store", Case.Sensitive);
        csText.ShouldContain("new ProcessedAddVehicle", Case.Sensitive);
    }

    [Fact]
    public void AddVehicleHandler_ShouldStartVehicleStream_AndEmit_VehicleAdded_WithAuthUserId_AndClockInstant()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Acceptance criteria: new event stream + VehicleAdded includes userId.
        csText.ShouldContain("new VehicleAdded(", Case.Sensitive);
        csText.ShouldContain("auth.UserId", Case.Sensitive);
        csText.ShouldContain("clock.GetCurrentInstant()", Case.Sensitive);

        // Stream should be started for a new vehicle stream.
        csText.ShouldContain("StartStream<Vehicle>", Case.Sensitive);
        csText.ShouldContain("vehicle-", Case.Sensitive);
    }

    [Fact]
    public void UpdateVehicleHandler_ShouldAppend_VehicleUpdated_WithAuthUserId_AndClockInstant()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Acceptance criteria: update emits VehicleUpdated.
        csText.ShouldContain("new VehicleUpdated(", Case.Sensitive);
        csText.ShouldContain("auth.UserId", Case.Sensitive);
        csText.ShouldContain("clock.GetCurrentInstant()", Case.Sensitive);

        // Existing stream should be appended to.
        csText.ShouldContain("session.Events.Append", Case.Sensitive);
        csText.ShouldContain("vehicle-", Case.Sensitive);
    }

    [Fact]
    public void VehicleProjection_ShouldApply_VehicleAdded_And_VehicleUpdated_IntoVehicleReadModel()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Read model existence + core fields (API contract)
        csText.ShouldContain("class VehicleReadModel", Case.Sensitive);
        csText.ShouldContain("Guid Id", Case.Sensitive);
        csText.ShouldContain("string UserId", Case.Sensitive);
        csText.ShouldContain("string Name", Case.Sensitive);
        csText.ShouldContain("string? Make", Case.Sensitive);
        csText.ShouldContain("string? Model", Case.Sensitive);
        csText.ShouldContain("int? Year", Case.Sensitive);
        csText.ShouldContain("string? PhotoUrl", Case.Sensitive);
        csText.ShouldContain("string? PhotoThumbnailUrl", Case.Sensitive);
        csText.ShouldContain("Instant CreatedAt", Case.Sensitive);
        csText.ShouldContain("Instant UpdatedAt", Case.Sensitive);

        // Projection mapping
        csText.ShouldContain("class VehicleProjection", Case.Sensitive);
        csText.ShouldContain("SingleStreamProjection<VehicleReadModel", Case.Sensitive);
        csText.ShouldContain("Lifecycle = ProjectionLifecycle.Inline", Case.Sensitive);

        csText.ShouldContain("VehicleReadModel Create(VehicleAdded", Case.Insensitive);
        csText.ShouldContain("ProjectEvent<VehicleUpdated>", Case.Sensitive);

        // Reflect updates in read model
        csText.ShouldContain("rm.Name = e.Name", Case.Sensitive);
        csText.ShouldContain("rm.Make = e.Make", Case.Sensitive);
        csText.ShouldContain("rm.Model = e.Model", Case.Sensitive);
        csText.ShouldContain("rm.Year = e.Year", Case.Sensitive);
        csText.ShouldContain("rm.UpdatedAt = e.OccurredAt", Case.Sensitive);
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
