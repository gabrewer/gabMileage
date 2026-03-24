using System.Text.Json.Serialization;

namespace gabMileage.Gateway.Auth;

public sealed record WorkOsTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn,
    [property: JsonPropertyName("token_type")] string? TokenType
);
