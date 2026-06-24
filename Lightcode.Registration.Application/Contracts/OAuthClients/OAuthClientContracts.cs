using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.Contracts.OAuthClients;

public sealed record OAuthClientTokenClaimValueDto(string Type, string Value);

public sealed record OAuthClientTokenConfigDto(
    int AccessTokenExpirationMinutes,
    int RefreshTokenExpirationDays,
    int MaxRefreshTokenUses,
    IReadOnlyList<OAuthClientTokenClaimValueDto> Values);

public sealed record CreateOAuthClientRequest(
    string? DisplayName,
    string? NotifyEmail,
    OAuthClientTokenConfigDto TokenConfig,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string>? RedirectUris = null,
    [property: JsonPropertyName("post_logout_redirect_uris")] IReadOnlyList<string>? PostLogoutRedirectUris = null,
    [property: JsonPropertyName("allowed_scopes")] IReadOnlyList<string>? AllowedScopes = null,
    [property: JsonPropertyName("require_consent")] bool RequireConsent = false);

public sealed record UpdateOAuthClientRequest(
    string? DisplayName,
    OAuthClientTokenConfigDto TokenConfig,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string>? RedirectUris = null,
    [property: JsonPropertyName("post_logout_redirect_uris")] IReadOnlyList<string>? PostLogoutRedirectUris = null,
    [property: JsonPropertyName("allowed_scopes")] IReadOnlyList<string>? AllowedScopes = null,
    [property: JsonPropertyName("require_consent")] bool? RequireConsent = null);

public sealed record OAuthClientDto(
    string Id,
    string ClientId,
    string? DisplayName,
    OAuthClientTokenConfigDto TokenConfig,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("post_logout_redirect_uris")] IReadOnlyList<string> PostLogoutRedirectUris,
    [property: JsonPropertyName("allowed_scopes")] IReadOnlyList<string> AllowedScopes,
    [property: JsonPropertyName("require_consent")] bool RequireConsent,
    bool Active,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record OAuthClientCreatedDto(
    string Id,
    string ClientId,
    string ClientSecret,
    string? DisplayName,
    OAuthClientTokenConfigDto TokenConfig,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("post_logout_redirect_uris")] IReadOnlyList<string> PostLogoutRedirectUris,
    [property: JsonPropertyName("allowed_scopes")] IReadOnlyList<string> AllowedScopes,
    [property: JsonPropertyName("require_consent")] bool RequireConsent);
