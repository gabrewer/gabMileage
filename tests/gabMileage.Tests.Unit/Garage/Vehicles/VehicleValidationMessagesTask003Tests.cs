using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Vehicles;

public sealed class VehicleValidationMessagesTask003Tests
{
    [Fact]
    public void VehicleInput_NameRequired_ShouldUseBriefMessage()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "VehicleInput.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Sprint 02 brief: Name invalid (blank/trimmed empty) -> "Your vehicle needs a name"
        text.ShouldContain("Your vehicle needs a name", Case.Sensitive);
    }

    [Fact]
    public void VehicleInput_YearInvalid_ShouldUseBriefMessage()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "VehicleInput.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Sprint 02 brief: Year invalid (out of range / not 4-digit) -> "Enter a valid year"
        text.ShouldContain("Enter a valid year", Case.Sensitive);
    }

    [Fact]
    public void VehicleInput_MakeTooLong_ShouldUseBriefMessage()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "VehicleInput.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Sprint 02 brief: Make > 50 -> "Make is too long"
        text.ShouldContain("Make is too long", Case.Sensitive);
    }

    [Fact]
    public void VehicleInput_ModelTooLong_ShouldUseBriefMessage()
    {
        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "VehicleInput.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        // Sprint 02 brief: Model > 50 -> "Model is too long"
        text.ShouldContain("Model is too long", Case.Sensitive);
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
