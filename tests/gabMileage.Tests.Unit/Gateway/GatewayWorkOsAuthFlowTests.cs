using System.Text;
using Shouldly;

namespace gabMileage.Tests.Unit.Gateway;

public sealed class GatewayWorkOsAuthFlowTests
{
    [Fact]
    public void Gateway_ShouldMap_LoginEndpoint_AndPreserveReturnTo_WithSafeRelativeValidation()
    {
        var root = RepoRoot.Find();
        var gatewayDir = Path.Combine(root, "gabMileage.Gateway");
        Directory.Exists(gatewayDir).ShouldBeTrue($"Expected Gateway directory at '{gatewayDir}'.");

        var csText = ReadAllCsText(gatewayDir);

        csText.ShouldContain("/api/auth/login", Case.Sensitive);
        csText.ShouldContain("returnTo", Case.Insensitive);

        // Contract requires explicit validation that returnTo is a safe *relative* URL.
        // Accept common patterns, but require at least one explicit "relative" validation clue.
        var hasRelativeValidationPattern =
            csText.Contains("UriKind.Relative", StringComparison.Ordinal) ||
            csText.Contains("IsLocalUrl", StringComparison.OrdinalIgnoreCase) ||
            csText.Contains("Uri.TryCreate", StringComparison.Ordinal);

        hasRelativeValidationPattern.ShouldBeTrue(
            "Expected /api/auth/login to validate returnTo is a safe relative URL (e.g., Uri.TryCreate(..., UriKind.Relative) or Url.IsLocalUrl)."
        );

        // The login endpoint must redirect (302) to WorkOS hosted auth.
        // Ensure the implementation references WorkOS and performs a redirect.
        csText.ShouldContain("workos", Case.Insensitive);

        var hasRedirectPattern =
            csText.Contains("Redirect(", StringComparison.Ordinal) ||
            csText.Contains("Results.Redirect", StringComparison.Ordinal) ||
            csText.Contains("TypedResults.Redirect", StringComparison.Ordinal);

        hasRedirectPattern.ShouldBeTrue("Expected /api/auth/login to issue an HTTP redirect to WorkOS.");
    }

    [Fact]
    public void Gateway_Callback_ShouldSet_GabSessionCookie_WithHttpOnlySecureSameSiteLax_AndRedirectToAuthRedirect()
    {
        var root = RepoRoot.Find();
        var gatewayDir = Path.Combine(root, "gabMileage.Gateway");

        var csText = ReadAllCsText(gatewayDir);

        csText.ShouldContain("/api/auth/callback", Case.Sensitive);

        // Cookie contract from docs/api + docs/domain
        csText.ShouldContain("gab_session", Case.Sensitive);

        // Require explicit cookie security flags.
        csText.ShouldContain("HttpOnly", Case.Insensitive);
        csText.ShouldContain("Secure", Case.Insensitive);
        csText.ShouldContain("SameSite", Case.Insensitive);
        csText.ShouldContain("Lax", Case.Insensitive);

        // Callback success must redirect to the frontend loading screen route.
        csText.ShouldContain("/auth/redirect", Case.Sensitive);
        csText.ShouldContain("returnTo", Case.Insensitive);
    }

    [Fact]
    public void Gateway_Logout_ShouldClearCookie_AndRedirectToLogin_With303SeeOther()
    {
        var root = RepoRoot.Find();
        var gatewayDir = Path.Combine(root, "gabMileage.Gateway");

        var csText = ReadAllCsText(gatewayDir);

        csText.ShouldContain("/api/auth/logout", Case.Sensitive);
        csText.ShouldContain("gab_session", Case.Sensitive);

        // Must clear cookie (Delete or expired cookie pattern).
        var hasCookieClearPattern =
            csText.Contains("Cookies.Delete", StringComparison.OrdinalIgnoreCase) ||
            csText.Contains("Response.Cookies.Delete", StringComparison.OrdinalIgnoreCase) ||
            csText.Contains("Expires", StringComparison.OrdinalIgnoreCase) ||
            csText.Contains("MaxAge", StringComparison.OrdinalIgnoreCase);

        hasCookieClearPattern.ShouldBeTrue(
            "Expected /api/auth/logout to clear the gab_session cookie (Delete or expired Set-Cookie)."
        );

        // Contract: 303 See Other + Location: /login
        csText.ShouldContain("/login", Case.Sensitive);

        var has303Pattern =
            csText.Contains("303", StringComparison.Ordinal) ||
            csText.Contains("Status303SeeOther", StringComparison.Ordinal) ||
            csText.Contains("SeeOther", StringComparison.Ordinal);

        has303Pattern.ShouldBeTrue("Expected /api/auth/logout to redirect using 303 See Other.");
    }

    [Fact]
    public void Gateway_ShouldConfigure_AuthenticationAndAuthorization_AndProtectReverseProxy()
    {
        var root = RepoRoot.Find();
        var gatewayDir = Path.Combine(root, "gabMileage.Gateway");

        var csText = ReadAllCsText(gatewayDir);

        // The Gateway owns JWT validation and should use ASP.NET Core auth middleware.
        csText.ShouldContain("AddAuthentication", Case.Sensitive);
        csText.ShouldContain("AddAuthorization", Case.Sensitive);
        csText.ShouldContain("UseAuthentication", Case.Sensitive);
        csText.ShouldContain("UseAuthorization", Case.Sensitive);

        // /api/v1/* requires auth; enforce via reverse proxy auth requirement.
        csText.ShouldContain("RequireAuthorization", Case.Sensitive);
    }

    [Fact]
    public void Gateway_ShouldForward_XUserId_Header_ToApi_WhenSessionIsValid()
    {
        var root = RepoRoot.Find();
        var gatewayDir = Path.Combine(root, "gabMileage.Gateway");

        var csText = ReadAllCsText(gatewayDir);

        // Internal contract: Gateway -> Api sends X-User-Id derived from JWT sub claim.
        csText.ShouldContain("X-User-Id", Case.Sensitive);
        csText.ShouldContain("sub", Case.Insensitive);
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

            throw new InvalidOperationException(
                "Could not locate repo root by walking up from AppContext.BaseDirectory."
            );
        }
    }
}
