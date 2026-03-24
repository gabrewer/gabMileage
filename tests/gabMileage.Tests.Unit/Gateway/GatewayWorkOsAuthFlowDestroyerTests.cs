using Shouldly;

namespace gabMileage.Tests.Unit.Gateway;

public sealed class GatewayWorkOsAuthFlowDestroyerTests
{
    [Fact]
    public void Program_ShouldNotDisable_JwtSignatureValidation_WhenAuthorityIsMissing()
    {
        // Threat model:
        // If ValidateIssuerSigningKey can be disabled based on config (e.g. missing authority),
        // an attacker can forge/"alg=none" a JWT with any sub and bypass auth.
        var root = RepoRoot.Find();
        var programCs = Path.Combine(root, "gabMileage.Gateway", "Program.cs");
        File.Exists(programCs).ShouldBeTrue($"Expected '{programCs}' to exist.");

        var text = File.ReadAllText(programCs);

        // Failing indicator: signature validation is conditional on JwtAuthority being set.
        text.ShouldNotContain("ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace", Case.Sensitive);
        text.ShouldNotContain("ValidateIssuerSigningKey = !", Case.Sensitive);

        // Positive indicator: signature validation should be explicitly enabled.
        // (Either via Authority metadata or explicit IssuerSigningKey(s).)
        var hasExplicitSigningValidation =
            text.Contains("ValidateIssuerSigningKey = true", StringComparison.Ordinal) ||
            text.Contains("IssuerSigningKey", StringComparison.Ordinal) ||
            text.Contains("IssuerSigningKeys", StringComparison.Ordinal) ||
            text.Contains("options.Authority", StringComparison.Ordinal);

        hasExplicitSigningValidation.ShouldBeTrue(
            "Expected JWT signature validation to be enabled in all configurations (authority or signing keys)."
        );
    }

    [Fact]
    public void Callback_ShouldRequire_State_AndValidate_Nonce_ToPrevent_LoginCsrf()
    {
        // Threat model: login CSRF/session swapping.
        // If /api/auth/callback accepts a code without requiring the state+nonce roundtrip,
        // an attacker can cause the victim to be logged in as the attacker's WorkOS account.
        var root = RepoRoot.Find();
        var endpointsCs = Path.Combine(root, "gabMileage.Gateway", "Auth", "GatewayAuthEndpoints.cs");
        File.Exists(endpointsCs).ShouldBeTrue($"Expected '{endpointsCs}' to exist.");

        var text = File.ReadAllText(endpointsCs);

        text.ShouldContain("/api/auth/callback", Case.Sensitive);

        // State must be required, not optional.
        var makesStateOptional = text.Contains("if (!string.IsNullOrWhiteSpace(state))", StringComparison.Ordinal);
        makesStateOptional.ShouldBeFalse(
            "State appears optional. Callback should reject missing/empty state to prevent login CSRF."
        );

        // Nonce must be validated (cookie vs state) on every callback.
        text.ShouldContain("GabAuthNonce", Case.Insensitive);
        text.ShouldContain("Invalid state nonce", Case.Insensitive);
    }

    [Fact]
    public void Login_ShouldReject_PercentEncoded_SchemeRelativeReturnTo_ToPrevent_OpenRedirect()
    {
        // Threat model: open redirect via percent-encoding.
        // Example: returnTo="/%2f%2fevil.com" is a relative URL, but if the frontend decodes and navigates,
        // it becomes "//evil.com" (scheme-relative) and leaves the origin.
        var root = RepoRoot.Find();
        var endpointsCs = Path.Combine(root, "gabMileage.Gateway", "Auth", "GatewayAuthEndpoints.cs");

        var text = File.ReadAllText(endpointsCs);

        // We expect explicit defense-in-depth against encoded "//".
        // Either decode then validate, or explicitly reject "%2f%2f" patterns.
        var hasEncodedSlashDefense =
            text.Contains("%2f%2f", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Uri.UnescapeDataString", StringComparison.Ordinal) ||
            text.Contains("WebUtility.UrlDecode", StringComparison.Ordinal);

        hasEncodedSlashDefense.ShouldBeTrue(
            "Expected returnTo validation to defend against percent-encoded scheme-relative redirects (e.g., %2f%2f)."
        );
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
