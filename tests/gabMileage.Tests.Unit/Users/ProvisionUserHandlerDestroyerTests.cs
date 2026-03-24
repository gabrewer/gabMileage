using Shouldly;

namespace gabMileage.Tests.Unit.Users;

public sealed class ProvisionUserHandlerDestroyerTests
{
    [Fact]
    public void Api_ProvisionEndpoint_MustDeriveUserId_FromAuthenticatedSubject_NotRequestBody()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var provisionEndpoint = ExtractBlock(text, "MapPost(\"/api/v1/users/provision\"", ");");

        // Multi-tenancy invariant: the authenticated subject must be the ONLY source of user identity.
        // If the endpoint trusts the UserId coming from the body, user A can provision/overwrite user B.
        provisionEndpoint.ShouldContain("FindFirstValue(\"sub\")");
    }

    [Fact]
    public void Api_ProvisionEndpoint_ShouldRequireAuthorization()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        var text = File.ReadAllText(programCs);
        var provisionEndpoint = ExtractBlock(text, "MapPost(\"/api/v1/users/provision\"", ");");

        // If the API is intended to be called only behind the Gateway, it still must reject direct unauthenticated calls.
        // Enforce via RequireAuthorization() (or equivalent) to prevent anonymous provisioning.
        provisionEndpoint.ShouldContain("RequireAuthorization", Case.Insensitive);
    }

    [Fact]
    public void Api_MartenSchema_ShouldBeAppliedOnStartup_InDevelopment()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        var text = File.ReadAllText(programCs);

        // Acceptance criteria says schema is created/migrated *on startup* in dev.
        // AutoCreate alone can defer creation until first usage; enforce explicit apply on startup.
        var hasDevGate = text.Contains("IsDevelopment()", StringComparison.Ordinal);
        hasDevGate.ShouldBeTrue("Expected dev-only gate for schema apply.");

        var hasExplicitApplyOnStartup =
            text.Contains("ApplyAllDatabaseChangesOnStartup", StringComparison.Ordinal) ||
            text.Contains("ApplyAllConfiguredChangesToDatabase", StringComparison.Ordinal) ||
            text.Contains("ApplyAllConfiguredChangesToDatabaseAsync", StringComparison.Ordinal);

        hasExplicitApplyOnStartup.ShouldBeTrue(
            "Expected Marten to explicitly apply/migrate schema on startup in Development (e.g., ApplyAllDatabaseChangesOnStartup()).");
    }

    [Fact]
    public void Api_ShouldNotDefine_A_JasperFxEvents_EventsShim_Type()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");

        // Namespace squatting against a library namespace is a brittle footgun:
        // it can silently change meaning of using directives and can break on package upgrades.
        var allText = ReadAllCsText(apiDir);
        allText.ShouldNotContain("namespace JasperFx.Events", Case.Insensitive);
        allText.ShouldNotContain("class Events", Case.Insensitive);
    }

    private static string ReadAllCsText(string dir)
    {
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(0, $"Expected at least one .cs file under '{dir}'.");

        return string.Join("\n\n", files.Select(File.ReadAllText));
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
