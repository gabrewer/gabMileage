using Shouldly;

namespace gabMileage.Tests.Unit.Api;

public sealed class ApiAuthContextAuthorizationBoundaryTests
{
    [Fact]
    public void Api_ShouldDefine_AuthContext_WithUserId()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        Directory.Exists(apiDir).ShouldBeTrue($"Expected API directory at '{apiDir}'.");

        var csText = ReadAllCsText(apiDir);

        var definesAuthContextType =
            csText.Contains("record AuthContext", StringComparison.Ordinal) ||
            csText.Contains("sealed record AuthContext", StringComparison.Ordinal) ||
            csText.Contains("class AuthContext", StringComparison.Ordinal) ||
            csText.Contains("sealed class AuthContext", StringComparison.Ordinal);

        definesAuthContextType.ShouldBeTrue(
            "Expected Api to define an AuthContext type for consistent access to authenticated user identity (see docs/domain + docs/api)."
        );

        csText.ShouldContain("UserId", Case.Sensitive);
    }

    [Fact]
    public void Api_ShouldAuthenticate_UsingGatewayForwardedHeader_XUserId_InAuthHandler()
    {
        var root = RepoRoot.Find();
        var handlerPath = Path.Combine(root, "gabMileage.Api", "Auth", "GatewayHeaderAuthenticationHandler.cs");
        File.Exists(handlerPath).ShouldBeTrue($"Expected '{handlerPath}' to exist.");

        var text = File.ReadAllText(handlerPath);

        // Contract: Gateway -> Api forwards WorkOS user id as X-User-Id header.
        text.ShouldContain("X-User-Id", Case.Sensitive);

        // Require at least one explicit request header read pattern (handler reads header and constructs the principal).
        var hasHeaderReadPattern =
            text.Contains("Request.Headers[\"X-User-Id\"]", StringComparison.Ordinal) ||
            text.Contains("Headers[\"X-User-Id\"]", StringComparison.Ordinal) ||
            text.Contains("Headers.TryGetValue(\"X-User-Id\"", StringComparison.Ordinal) ||
            text.Contains("TryGetValue(\"X-User-Id\"", StringComparison.Ordinal);

        hasHeaderReadPattern.ShouldBeTrue(
            "Expected Api to read X-User-Id in the authentication handler and use it to build the authenticated principal."
        );
    }

    [Fact]
    public void Api_ShouldConfigure_AuthenticationAndAuthorization_Middleware_SoUnauthenticatedCallsGet401()
    {
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Api", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected API Program.cs at '{programCs}'.");

        var text = File.ReadAllText(programCs);

        // Baseline: endpoints marked RequireAuthorization must be enforced at runtime.
        text.ShouldContain("AddAuthentication", Case.Sensitive);
        text.ShouldContain("AddAuthorization", Case.Sensitive);
        text.ShouldContain("UseAuthentication", Case.Sensitive);
        text.ShouldContain("UseAuthorization", Case.Sensitive);
    }

    [Fact]
    public void Api_ShouldMap_GarageSummary_Endpoint_AndProtectIt()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        var csText = ReadAllCsText(apiDir);

        csText.ShouldContain("/api/v1/garage/summary", Case.Sensitive);

        // The endpoint itself can be protected directly OR via a /api/v1/garage group.
        var hasProtectedGarageGroup =
            csText.Contains("MapGroup(\"/api/v1/garage\"", StringComparison.Ordinal) &&
            csText.Contains("RequireAuthorization", StringComparison.Ordinal);

        var endpointBlock = ExtractBlock(csText, "MapGet(\"/api/v1/garage/summary\"", ");");
        var hasEndpointRequireAuth = endpointBlock.Contains("RequireAuthorization", StringComparison.OrdinalIgnoreCase);

        (hasEndpointRequireAuth || hasProtectedGarageGroup).ShouldBeTrue(
            "Expected GET /api/v1/garage/summary to require authentication (401 when unauthenticated)."
        );
    }

    [Fact]
    public void GarageSummary_ShouldBeUserScoped_AndNotAcceptCrossUserIdentifiers()
    {
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");
        var csText = ReadAllCsText(apiDir);

        var endpointBlock = ExtractBlock(csText, "MapGet(\"/api/v1/garage/summary\"", ");");

        // Cross-user access should not be possible by providing a userId parameter.
        endpointBlock.ShouldNotContain("{userId}", Case.Insensitive);
        endpointBlock.ShouldNotContain("string userId", Case.Insensitive);
        endpointBlock.ShouldNotContain("FromRoute", Case.Insensitive);
        endpointBlock.ShouldNotContain("FromQuery", Case.Insensitive);

        // Must scope response to the authenticated user.
        endpointBlock.ShouldContain("authContext", Case.Insensitive);

        var scopesUserIdToAuthContext =
            endpointBlock.Contains("authContext.UserId", StringComparison.Ordinal) &&
            (endpointBlock.Contains("UserId = authContext.UserId", StringComparison.Ordinal) ||
             endpointBlock.Contains("userId = authContext.UserId", StringComparison.Ordinal));

        scopesUserIdToAuthContext.ShouldBeTrue(
            "Expected GarageSummary to return data scoped to the current authenticated user (userId derived from AuthContext)."
        );
    }

    private static string ReadAllCsText(string dir)
    {
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(0, $"Expected at least one .cs file under '{dir}'.");

        return string.Join("\n\n", files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(File.ReadAllText));
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
