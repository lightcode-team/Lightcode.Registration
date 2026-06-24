using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.Contracts.Auth;

/// <summary>Pedido unificado de token OAuth2; o tenant deve ser enviado no cabeçalho HTTP (ex.: <c>X-Tenant-Id</c>).</summary>
public sealed record TokenRequest(
    [property: JsonPropertyName("grant_type")] string? GrantType,
    string? Username,
    string? Password,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("client_id")] string? ClientId,
    [property: JsonPropertyName("client_secret")] string? ClientSecret,
    string? Code = null,
    [property: JsonPropertyName("redirect_uri")] string? RedirectUri = null,
    [property: JsonPropertyName("code_verifier")] string? CodeVerifier = null,
    string? Scope = null);
