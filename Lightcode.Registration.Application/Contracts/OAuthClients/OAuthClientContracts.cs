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
    OAuthClientTokenConfigDto TokenConfig);

public sealed record UpdateOAuthClientRequest(
    string? DisplayName,
    OAuthClientTokenConfigDto TokenConfig);

public sealed record OAuthClientDto(
    string Id,
    string ClientId,
    string? DisplayName,
    OAuthClientTokenConfigDto TokenConfig,
    bool Active,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record OAuthClientCreatedDto(
    string Id,
    string ClientId,
    string ClientSecret,
    string? DisplayName,
    OAuthClientTokenConfigDto TokenConfig);
