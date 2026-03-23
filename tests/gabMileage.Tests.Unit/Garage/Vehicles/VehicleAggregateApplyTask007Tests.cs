using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Vehicles;

public sealed class VehicleAggregateApplyTask007Tests
{
    [Fact]
    public void VehicleAggregate_ShouldCarryCoreProfileFields_AndApplyVehicleAddedIntoState()
    {
        // Sprint 02 domain model (docs/domain/garage-vehicles.md): Vehicle aggregate stores profile fields
        // and Apply(VehicleAdded) sets them.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Vehicle.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Fields on aggregate
        text.ShouldContain("string Name", Case.Sensitive);
        text.ShouldContain("string? Make", Case.Sensitive);
        text.ShouldContain("string? Model", Case.Sensitive);
        text.ShouldContain("int? Year", Case.Sensitive);
        text.ShouldContain("Instant CreatedAt", Case.Sensitive);
        text.ShouldContain("Instant UpdatedAt", Case.Sensitive);
        text.ShouldContain("Guid CommandId", Case.Sensitive);

        // Apply(VehicleAdded) must map core fields from event
        text.ShouldContain("void Apply(VehicleAdded", Case.Sensitive);
        text.ShouldContain("Name = e.Name", Case.Sensitive);
        text.ShouldContain("Make = e.Make", Case.Sensitive);
        text.ShouldContain("Model = e.Model", Case.Sensitive);
        text.ShouldContain("Year = e.Year", Case.Sensitive);
        text.ShouldContain("CreatedAt = e.OccurredAt", Case.Sensitive);
        text.ShouldContain("UpdatedAt = e.OccurredAt", Case.Sensitive);
        text.ShouldContain("CommandId = e.CommandId", Case.Sensitive);
    }

    [Fact]
    public void VehicleAggregate_ApplyVehicleUpdated_ShouldUpdateProfileFields_ButNotOwnerOrCreatedAt()
    {
        // Domain invariant: owner is immutable after creation; CreatedAt is immutable.
        // Apply(VehicleUpdated) updates profile fields + UpdatedAt only.
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Vehicle.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        var applyUpdated = ExtractMethodBlock(text, "void Apply(VehicleUpdated");

        applyUpdated.ShouldContain("Name = e.Name", Case.Sensitive);
        applyUpdated.ShouldContain("Make = e.Make", Case.Sensitive);
        applyUpdated.ShouldContain("Model = e.Model", Case.Sensitive);
        applyUpdated.ShouldContain("Year = e.Year", Case.Sensitive);
        applyUpdated.ShouldContain("UpdatedAt = e.OccurredAt", Case.Sensitive);

        // Must not overwrite immutable fields on update.
        // Note: Apply(VehicleAdded) legitimately sets CreatedAt/CommandId; scope assertions only to the Apply(VehicleUpdated) method.
        applyUpdated.ShouldNotContain("UserId = e.UserId", Case.Sensitive);
        applyUpdated.ShouldNotContain("CreatedAt =", Case.Sensitive);
        applyUpdated.ShouldNotContain("CommandId =", Case.Sensitive);
    }

    private static string ExtractMethodBlock(string text, string methodSignatureStartsWith)
    {
        var signatureIndex = text.IndexOf(methodSignatureStartsWith, StringComparison.Ordinal);
        signatureIndex.ShouldBeGreaterThanOrEqualTo(0, $"Expected to find method signature starting with '{methodSignatureStartsWith}'.");

        var openBraceIndex = text.IndexOf('{', signatureIndex);
        openBraceIndex.ShouldBeGreaterThan(signatureIndex, "Expected method body '{' after signature.");

        var depth = 0;
        for (var i = openBraceIndex; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            if (text[i] == '}') depth--;
            if (depth == 0)
            {
                return text.Substring(signatureIndex, i - signatureIndex + 1);
            }
        }

        throw new InvalidOperationException($"Unbalanced braces while extracting method '{methodSignatureStartsWith}'.");
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
