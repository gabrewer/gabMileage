using Shouldly;

namespace gabMileage.Tests.Unit.E2E;

public sealed class Task007PlaywrightMustRunDestroyerTests
{
    [Fact]
    public void Task007_AddVehicleHappyPath_PlaywrightSpec_MustNotBeSilentlySkipped()
    {
        // Threat model:
        // - Acceptance requires an automated UI-level check.
        // - If the Playwright spec is gated behind env vars and calls test.skip(...), CI can pass with *zero* UI coverage.
        // Guardrail:
        // - The TASK-007 happy-path spec should be runnable in CI without secrets (e.g., via test auth, seeded user, or mocked auth layer).
        // - This test intentionally fails if the spec contains runtime skip gates.
        var root = RepoRoot.Find();
        var specPath = Path.Combine(root, "tests", "gabMileage.Tests.E2E", "TASK-007", "add-vehicle-dashboard-navigation.spec.ts");
        File.Exists(specPath).ShouldBeTrue($"Expected Playwright spec at '{specPath}'.");

        var text = File.ReadAllText(specPath);

        text.ShouldNotContain("test.skip", Case.Sensitive,
            "TASK-007 Playwright spec is currently gated behind env vars (test.skip). That makes UI coverage optional. Provide a CI-runnable auth strategy or remove the skip.");
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
