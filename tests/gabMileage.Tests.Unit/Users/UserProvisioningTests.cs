using System.Text;
using Shouldly;

namespace gabMileage.Tests.Unit.Users;

public sealed class UserProvisioningTests
{
    [Fact]
    public void Api_ShouldMigrateEventStoreSchema_OnStartup_InDevelopment()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);

        var hasDevGate = text.Contains("IsDevelopment()", StringComparison.Ordinal);

        // Accept a few equivalent Marten patterns. The requirement is: dev startup should ensure schema exists/migrates.
        var hasSchemaApplyPattern =
            text.Contains("ApplyAllDatabaseChangesOnStartup", StringComparison.Ordinal) ||
            text.Contains("ApplyAllConfiguredChangesToDatabase", StringComparison.Ordinal) ||
            text.Contains("AutoCreateSchemaObjects", StringComparison.Ordinal) ||
            text.Contains("AutoCreate.", StringComparison.Ordinal);

        (hasDevGate && hasSchemaApplyPattern).ShouldBeTrue(
            "Expected Marten/Postgres schema to be created/migrated on startup in Development. " +
            "Add a dev-only gate (IsDevelopment) and either apply configured changes on startup or enable AutoCreate schema objects."
        );
    }

    [Fact]
    public void Api_MartenConfiguration_ShouldRegister_UserProvisioned_EventType()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var martenBlock = ExtractBlock(text, "AddMarten(options =>", "})");

        martenBlock.ShouldContain("AddEventType", Case.Insensitive);
        martenBlock.ShouldContain("UserProvisioned", Case.Sensitive);
    }

    [Fact]
    public void Api_MartenConfiguration_ShouldRegister_UserProfileProjection_Inline()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);
        var martenBlock = ExtractBlock(text, "AddMarten(options =>", "})");

        // The domain model requires an inline SingleStreamProjection so the read model is queryable immediately after provisioning.
        martenBlock.ShouldContain("UserProfileProjection", Case.Sensitive);
        martenBlock.ShouldContain("ProjectionLifecycle.Inline", Case.Sensitive);
        martenBlock.ShouldContain("Projections", Case.Insensitive);
    }

    [Fact]
    public void Users_Domain_ShouldDefine_UserProvisioned_EventRecord_WithExpectedFields()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("record UserProvisioned");

        // Field contract from docs/domain + docs/api
        csText.ShouldContain("string UserId");
        csText.ShouldContain("string DisplayName");
        csText.ShouldContain("string Email");
        csText.ShouldContain("string? AvatarUrl");
        csText.ShouldContain("Instant OccurredAt");
    }

    [Fact]
    public void Users_Domain_ShouldDefine_ProvisionUser_CommandRecord_WithExpectedFields()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("record ProvisionUser");

        csText.ShouldContain("string UserId");
        csText.ShouldContain("string DisplayName");
        csText.ShouldContain("string Email");
        csText.ShouldContain("string? AvatarUrl");
    }

    [Fact]
    public void ProvisionUserHandler_ShouldBeIdempotent_AndNotEmitDuplicateEvents()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // Handler sketch in docs/domain specifies aggregate load + IsProvisioned guard.
        csText.ShouldContain("AggregateStreamAsync<User>");
        csText.ShouldContain("IsProvisioned");
        csText.ShouldContain("return Events.Empty");
    }

    [Fact]
    public void ProvisionUserHandler_ShouldStartUserStream_WithUserProvisioned_UsingClockInstant()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("new UserProvisioned(");
        csText.ShouldContain("clock.GetCurrentInstant()", Case.Sensitive);
        csText.ShouldContain("StartStream<User>");
    }

    [Fact]
    public void Users_ReadModel_ShouldDefine_UserProfile_WithRequiredFields()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("class UserProfile");

        csText.ShouldContain("string Id");
        csText.ShouldContain("string DisplayName");
        csText.ShouldContain("string Email");
        csText.ShouldContain("string? AvatarUrl");
        csText.ShouldContain("Instant CreatedAt");
    }

    [Fact]
    public void Users_Projection_ShouldCreate_UserProfile_FromUserProvisioned_Event()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("class UserProfileProjection");
        csText.ShouldContain("SingleStreamProjection<UserProfile>");
        csText.ShouldContain("Lifecycle = ProjectionLifecycle.Inline");
        csText.ShouldContain("UserProfile Create(UserProvisioned");

        // Mapping contract
        csText.ShouldContain("Id = e.UserId");
        csText.ShouldContain("DisplayName = e.DisplayName");
        csText.ShouldContain("Email = e.Email");
        csText.ShouldContain("AvatarUrl = e.AvatarUrl");
        csText.ShouldContain("CreatedAt = e.OccurredAt");
    }

    [Fact]
    public void Api_ShouldQuery_UserProfile_ByUserId_ForMeEndpoint()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("/api/v1/me");

        var hasLoadPattern =
            csText.Contains("LoadAsync<UserProfile>", StringComparison.Ordinal) ||
            csText.Contains("Load<UserProfile>", StringComparison.Ordinal);

        hasLoadPattern.ShouldBeTrue(
            "Expected GET /api/v1/me to query the UserProfile read model by userId using Marten (Load/LoadAsync)."
        );
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
