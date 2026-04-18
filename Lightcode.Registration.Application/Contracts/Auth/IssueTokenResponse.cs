using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.Contracts.Auth;

public sealed record IssueTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresInSeconds);
