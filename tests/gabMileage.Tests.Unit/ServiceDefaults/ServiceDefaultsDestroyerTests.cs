using Shouldly;

namespace gabMileage.Tests.Unit.ServiceDefaults;

public sealed class ServiceDefaultsDestroyerTests
{
    [Fact]
    public void StartupHealthCheckValidator_ShouldOnlyValidateBasicLiveChecks_NotAllChecks()
    {
        var root = RepoRoot.Find();
        var validator = Path.Combine(root, "gabMileage.ServiceDefaults", "StartupHealthCheckValidator.cs");

        File.Exists(validator).ShouldBeTrue($"Expected StartupHealthCheckValidator at '{validator}'.");

        var text = File.ReadAllText(validator);

        // Destroyer: fail-fast startup is fine, but validating *all* health checks can deadlock distributed startup.
        // Example: Service A has a health check for Service B, and Service B has one for Service A.
        // If both services fail-fast on *all* checks, neither can start.
        // Expectation: validator should filter to 'live' (or equivalent) checks only.
        var usesPredicateOverload = text.Contains("CheckHealthAsync(", StringComparison.Ordinal) &&
                                   text.Contains("predicate", StringComparison.OrdinalIgnoreCase);

        var filtersLiveTag = text.Contains("\"live\"", StringComparison.Ordinal) ||
                             text.Contains("Tags", StringComparison.OrdinalIgnoreCase);

        (usesPredicateOverload || filtersLiveTag).ShouldBeTrue(
            "StartupHealthCheckValidator appears to validate ALL registered health checks. " +
            "This is brittle and can prevent services from ever starting when readiness checks depend on other services. " +
            "Filter startup validation to basic liveness checks only (e.g., tag 'live').");
    }

    [Fact]
    public void MapDefaultEndpoints_ShouldExposeHealthEndpoints_InNonDevelopmentEnvironments()
    {
        var root = RepoRoot.Find();
        var extensions = Path.Combine(root, "gabMileage.ServiceDefaults", "Class1.cs");

        File.Exists(extensions).ShouldBeTrue($"Expected ServiceDefaults extensions at '{extensions}'.");

        var text = File.ReadAllText(extensions);

        // Destroyer: Aspire health checks should be visible consistently; mapping endpoints only in Development
        // means staging/prod (and some CI environments) silently lose /health and /alive.
        text.ShouldNotContain("if (app.Environment.IsDevelopment())");
        text.ShouldContain("MapHealthChecks(HealthEndpointPath");
        text.ShouldContain("MapHealthChecks(AlivenessEndpointPath");
    }

    private static class RepoRoot
    {
        public static string Find()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                // In git worktrees, .git is a file; in normal checkouts it's a directory.
                if (File.Exists(Path.Combine(current.FullName, ".git")) ||
                    Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate repo root by walking up from AppContext.BaseDirectory.");
        }
    }
}
