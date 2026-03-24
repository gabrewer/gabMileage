using System.Text.Json;
using Shouldly;

namespace gabMileage.Tests.Unit.Foundation;

public sealed class AspireScaffoldDestroyerTests
{
    [Fact]
    public void Api_MartenConfiguration_ShouldExplicitlyEnable_NodaTimeSerialization()
    {
        // Why this exists:
        // - HTTP JSON options are configured for NodaTime, but Marten has its *own* serializer options.
        // - If events/documents contain Instant/LocalDate, Marten will throw at runtime unless its serializer is configured.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");

        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);

        var martenBlock = ExtractBlock(text, "AddMarten(options =>", "})");

        // Accept any of these explicit patterns; the key requirement is: Marten serializer knows NodaTime.
        var hasExplicitMartenNodaTime =
            martenBlock.Contains("UseSystemTextJsonForSerialization", StringComparison.Ordinal) ||
            martenBlock.Contains("ConfigureForNodaTime", StringComparison.Ordinal) ||
            martenBlock.Contains("NodaTime", StringComparison.Ordinal);

        hasExplicitMartenNodaTime.ShouldBeTrue(
            "Marten is configured, but there is no explicit NodaTime serializer configuration in the Marten setup. " +
            "This is a runtime-breaker once events/documents include Instant/LocalDate (HTTP JSON config is not enough)."
        );
    }

    [Fact]
    public void Gateway_YarpRoutes_ShouldBeExplicit_AndNotAccidentallyShadowApi()
    {
        var root = RepoRoot.Find();
        var appsettings = Path.Combine(root, "gabMileage.Gateway", "appsettings.json");

        File.Exists(appsettings).ShouldBeTrue($"Expected '{appsettings}'.");

        var json = File.ReadAllText(appsettings);
        using var doc = JsonDocument.Parse(json);

        var routes = doc.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes");

        routes.TryGetProperty("api", out var apiRoute).ShouldBeTrue("Expected ReverseProxy:Routes:api");
        apiRoute.GetProperty("Match").GetProperty("Path").GetString().ShouldBe("/api/{**catch-all}");

        routes.TryGetProperty("web", out var webRoute).ShouldBeTrue("Expected ReverseProxy:Routes:web");
        webRoute.GetProperty("Match").GetProperty("Path").GetString().ShouldBe("/{**catch-all}");

        // Extra guard: if someone ever changes the API match to a less specific pattern,
        // the catch-all web route could unintentionally shadow API traffic.
        apiRoute.GetProperty("Match").GetProperty("Path").GetString()!
            .StartsWith("/api/", StringComparison.Ordinal)
            .ShouldBeTrue("API route should keep a strict /api/* prefix.");
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
