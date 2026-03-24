using System.Xml.Linq;
using Shouldly;

namespace gabMileage.Tests.Unit.Foundation;

public sealed class SolutionScaffoldTests
{
    [Fact]
    public void SolutionFile_ShouldExist_AndContainRequiredProjects()
    {
        var root = RepoRoot.Find();
        var slnPath = Path.Combine(root, "gabMileage.sln");

        File.Exists(slnPath).ShouldBeTrue($"Expected solution file at '{slnPath}'.");

        var slnText = File.ReadAllText(slnPath);

        // The PRD lists these required projects.
        slnText.ShouldContain("gabMileage.AppHost");
        slnText.ShouldContain("gabMileage.ServiceDefaults");
        slnText.ShouldContain("gabMileage.Api");
        slnText.ShouldContain("gabMileage.Gateway");
        slnText.ShouldContain("gabMileage.Web");
    }

    [Fact]
    public void RequiredProjectDirectoriesAndCsprojFiles_ShouldExist()
    {
        var root = RepoRoot.Find();

        AssertProjectExists(root, "gabMileage.AppHost");
        AssertProjectExists(root, "gabMileage.ServiceDefaults");
        AssertProjectExists(root, "gabMileage.Api");
        AssertProjectExists(root, "gabMileage.Gateway");

        // Web is TanStack Start; acceptance criteria require it exists as a resource.
        Directory.Exists(Path.Combine(root, "gabMileage.Web"))
            .ShouldBeTrue("Expected a 'gabMileage.Web' directory at repo root.");
    }

    [Fact]
    public void ApiProject_ShouldReference_Marten_Wolverine_And_NodaTime()
    {
        var root = RepoRoot.Find();
        var apiProj = Path.Combine(root, "gabMileage.Api", "gabMileage.Api.csproj");

        File.Exists(apiProj).ShouldBeTrue($"Expected API project file at '{apiProj}'.");

        var doc = XDocument.Load(apiProj);
        var includes = doc
            .Descendants("PackageReference")
            .Select(x => (string?)x.Attribute("Include"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        includes.ShouldContain(x => x!.Contains("Marten", StringComparison.OrdinalIgnoreCase));
        includes.ShouldContain(x => x!.Contains("Wolverine", StringComparison.OrdinalIgnoreCase));
        includes.ShouldContain(x => x!.Equals("NodaTime", StringComparison.OrdinalIgnoreCase));

        // Needed to serialize Instant/LocalDate as specified by the API contract.
        includes.ShouldContain(x => x!.Contains("NodaTime.Serialization.SystemTextJson", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Api_ShouldConfigure_MartenForStringStreamIdentity_And_NodaTime()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");

        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("StreamIdentity.AsString");
        csText.ShouldContain("UseNodaTime()");
    }

    [Fact]
    public void Api_ShouldConfigure_JsonSerialization_ForInstantAndLocalDate()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");

        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        // The exact API contract requires ISO strings for Instant + LocalDate.
        // A common pattern is NodaTime.Serialization.SystemTextJson's ConfigureForNodaTime().
        csText.ShouldContain("ConfigureForNodaTime");
        csText.ShouldContain("Instant");
        csText.ShouldContain("LocalDate");
    }

    [Fact]
    public void Api_ShouldExpose_HealthAndAlive_Endpoints()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");

        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("MapHealthChecks(\"/health\"");
        csText.ShouldContain("MapHealthChecks(\"/alive\"");
    }

    [Fact]
    public void GatewayProject_ShouldReference_Yarp_AndHaveReverseProxyConfigToApi()
    {
        var root = RepoRoot.Find();
        var gatewayProj = Path.Combine(root, "gabMileage.Gateway", "gabMileage.Gateway.csproj");

        File.Exists(gatewayProj).ShouldBeTrue($"Expected Gateway project file at '{gatewayProj}'.");

        var doc = XDocument.Load(gatewayProj);
        var includes = doc
            .Descendants("PackageReference")
            .Select(x => (string?)x.Attribute("Include"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        includes.ShouldContain(x => x!.Equals("Yarp.ReverseProxy", StringComparison.OrdinalIgnoreCase));

        var gatewayDir = Path.GetDirectoryName(gatewayProj) ?? throw new InvalidOperationException();

        var appsettings = Path.Combine(gatewayDir, "appsettings.json");
        File.Exists(appsettings).ShouldBeTrue($"Expected Gateway reverse proxy config at '{appsettings}'.");

        var json = File.ReadAllText(appsettings);
        json.ShouldContain("ReverseProxy");

        // Must route Web -> Gateway -> Api; we expect an Api destination/cluster.
        json.ToLowerInvariant().ShouldContain("api");
    }

    [Fact]
    public void Gateway_ShouldExpose_HealthAndAlive_Endpoints()
    {
        var root = RepoRoot.Find();
        var gatewayDir = Path.Combine(root, "gabMileage.Gateway");

        Directory.Exists(gatewayDir).ShouldBeTrue($"Expected Gateway directory at '{gatewayDir}'.");

        var csText = ReadAllCsText(gatewayDir);
        csText.ShouldContain("MapHealthChecks(\"/health\"");
        csText.ShouldContain("MapHealthChecks(\"/alive\"");
    }

    [Fact]
    public void AppHost_ShouldDefine_Resources_ForApi_Gateway_Web_Postgres_And_Redis()
    {
        var root = RepoRoot.Find();
        var appHostDir = Path.Combine(root, "gabMileage.AppHost");
        var programCs = Path.Combine(appHostDir, "Program.cs");

        File.Exists(programCs).ShouldBeTrue($"Expected AppHost Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);

        text.ShouldContain("gabMileage.Api");
        text.ShouldContain("gabMileage.Gateway");
        text.ShouldContain("gabMileage.Web");

        // Resource expectations per acceptance criteria.
        text.ShouldContain("Postgres", Case.Insensitive);
        text.ShouldContain("Redis", Case.Insensitive);
    }

    private static void AssertProjectExists(string root, string projectName)
    {
        var projDir = Path.Combine(root, projectName);
        Directory.Exists(projDir).ShouldBeTrue($"Expected project directory '{projectName}' at '{projDir}'.");

        var csproj = Path.Combine(projDir, projectName + ".csproj");
        File.Exists(csproj).ShouldBeTrue($"Expected csproj '{projectName}.csproj' at '{csproj}'.");
    }

    private static string ReadAllCsText(string dir)
    {
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(0, $"Expected at least one .cs file under '{dir}'.");

        return string.Join("\n\n", files.Select(File.ReadAllText));
    }

    private static class RepoRoot
    {
        public static string Find()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                // This repo uses a .git *file* (not necessarily a directory) in the working tree.
                if (File.Exists(Path.Combine(current.FullName, ".git")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate repo root by walking up from AppContext.BaseDirectory.");
        }
    }
}
