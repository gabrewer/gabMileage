using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace gabMileage.Gateway.Auth;

public sealed class WorkOsClient(HttpClient http, IOptions<WorkOsOptions> options)
{
    public const string HttpClientName = "workos";

    private readonly WorkOsOptions _options = options.Value;

    public string BuildAuthorizeUrl(string state)
    {
        // Breadcrumbs (backend-builder)
        // What: Build a WorkOS hosted auth URL for the browser to navigate to.
        // Why: WorkOS owns the UI; our login endpoint is a simple 302.
        // Alternatives considered: rendering our own login UI (rejected per sprint brief).
        // Confidence: medium

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["state"] = state
        };

        var qs = string.Join("&", query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        return _options.AuthorizationEndpoint.Contains('?')
            ? $"{_options.AuthorizationEndpoint}&{qs}"
            : $"{_options.AuthorizationEndpoint}?{qs}";
    }

    public async Task<WorkOsTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["code"] = code
        }!);

        using var req = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = content
        };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        resp.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<WorkOsTokenResponse>(json) ??
               throw new InvalidOperationException("WorkOS token response could not be deserialized.");
    }

    public async Task<WorkOsTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken
        }!);

        using var req = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = content
        };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        resp.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<WorkOsTokenResponse>(json) ??
               throw new InvalidOperationException("WorkOS refresh response could not be deserialized.");
    }
}
