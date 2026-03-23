using Shouldly;

namespace gabMileage.Tests.Unit.Api;

public sealed class ApiAuthContextAuthorizationBoundaryDestroyerTests
{
    [Fact]
    public void Api_AuthContext_DI_ShouldDeriveUserId_FromAuthenticatedPrincipal_Sub_NotRawHeaders()
    {
        // Threat model:
        // - Today, every endpoint might correctly call RequireAuthorization().
        // - Tomorrow, a single AllowAnonymous/missing RequireAuthorization() is enough for header spoofing
        //   if AuthContext is populated directly from Request.Headers.
        //
        // Guardrail:
        // - AuthContext should be derived from HttpContext.User (the authenticated principal), not directly
        //   from gateway headers. The authentication handler can still use the gateway header to build the principal.
        var root = RepoRoot.Find();
        var apiDir = Path.Combine(root, "gabMileage.Api");

        var csText = ReadAllCsText(apiDir);

        // Baseline: AuthContext is registered via DI.
        csText.ShouldContain("AddScoped<AuthContext>", Case.Insensitive,
            "Expected AuthContext to be registered in DI so endpoints/handlers can consistently access the authenticated userId.");

        // We intentionally locate the AuthContext registration block(s) and assert the absence of header reads.
        var occurrences = FindAllIndexes(csText, "AddScoped<AuthContext>");
        occurrences.Count.ShouldBeGreaterThan(0, "Expected AuthContext to be registered in Api DI.");

        foreach (var start in occurrences)
        {
            var block = ExtractBraceBlock(csText, start);

            block.ShouldNotContain("Request.Headers", Case.Insensitive,
                "AuthContext must not be populated from raw request headers; derive from HttpContext.User instead.");
            block.ShouldNotContain("X-User-Id", Case.Insensitive,
                "AuthContext DI must not directly read X-User-Id; that belongs in the authentication handler only.");

            // Positive indicator: read from the authenticated principal.
            var hasPrincipalRead =
                block.Contains("HttpContext.User", StringComparison.OrdinalIgnoreCase) ||
                block.Contains("FindFirstValue(\"sub\")", StringComparison.Ordinal);

            hasPrincipalRead.ShouldBeTrue(
                "Expected AuthContext DI to read user id from HttpContext.User (e.g., FindFirstValue(\"sub\")) so it is only present after authentication.");
        }
    }

    [Fact]
    public void Api_GatewayHeaderAuthenticationHandler_ShouldReject_MultiValue_AndTrim_UserId()
    {
        // Threat model:
        // - Multiple header values can be collapsed into a comma-separated string via StringValues.ToString().
        // - Leading/trailing whitespace should not become part of an identity key.
        // - If these are accepted, stream IDs / document IDs can become ambiguous or attacker-controlled.
        var root = RepoRoot.Find();
        var handlerPath = Path.Combine(root, "gabMileage.Api", "Auth", "GatewayHeaderAuthenticationHandler.cs");
        File.Exists(handlerPath).ShouldBeTrue($"Expected '{handlerPath}' to exist.");

        var text = File.ReadAllText(handlerPath);

        // Positive indicators we expect in a hardened handler.
        // Any one of these patterns is acceptable as a defense-in-depth signal.
        var trims = text.Contains("Trim()", StringComparison.Ordinal);
        var checksCount = text.Contains("userIdHeader.Count", StringComparison.Ordinal) ||
                          text.Contains("StringValues", StringComparison.Ordinal) && text.Contains("Count", StringComparison.Ordinal);
        var rejectsComma = text.Contains("Contains(',')", StringComparison.Ordinal) ||
                           text.Contains("IndexOf(',')", StringComparison.Ordinal);

        (trims && (checksCount || rejectsComma)).ShouldBeTrue(
            "Expected GatewayHeaderAuthenticationHandler to defensively normalize/validate userId header values (trim + reject multi-values).");
    }

    private static IReadOnlyList<int> FindAllIndexes(string text, string needle)
    {
        var list = new List<int>();
        var idx = 0;
        while (true)
        {
            var found = text.IndexOf(needle, idx, StringComparison.Ordinal);
            if (found < 0) break;
            list.Add(found);
            idx = found + needle.Length;
        }

        return list;
    }

    private static string ReadAllCsText(string dir)
    {
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(0, $"Expected at least one .cs file under '{dir}'.");

        return string.Join("\n\n", files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(File.ReadAllText));
    }

    private static string ExtractBraceBlock(string text, int startIndex)
    {
        // Extract a balanced {...} block starting at or after startIndex.
        var open = text.IndexOf('{', startIndex);
        open.ShouldBeGreaterThanOrEqualTo(0, "Expected '{' after marker.");

        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text.Substring(open, i - open + 1);
            }
        }

        throw new InvalidOperationException("Unbalanced braces while extracting block.");
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
