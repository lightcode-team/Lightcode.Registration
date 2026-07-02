using Lightcode.Registration.Application.Contracts.OAuthClients;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.OAuthClients;

internal static class OAuthClientMapping
{
    public static OAuthClientDto ToDto(OAuthClient client) =>
        new(
            client.Id,
            client.ClientId,
            client.DisplayName,
            client.NotifyEmail,
            ToConfigDto(client.TokenConfig),
            client.RedirectUris,
            client.PostLogoutRedirectUris,
            client.AllowedScopes,
            client.RequireConsent,
            client.Active,
            client.CreatedAtUtc,
            client.UpdatedAtUtc);

    public static OAuthClientTokenConfigDto ToConfigDto(OAuthClientTokenConfiguration config) =>
        new(
            config.AccessTokenExpirationMinutes,
            config.RefreshTokenExpirationDays,
            config.MaxRefreshTokenUses,
            config.Values
                .Select(v => new OAuthClientTokenClaimValueDto(v.Type, v.Value))
                .ToList());

    public static OAuthClientTokenConfiguration ToEntity(OAuthClientTokenConfigDto dto) =>
        new()
        {
            AccessTokenExpirationMinutes = dto.AccessTokenExpirationMinutes,
            RefreshTokenExpirationDays = dto.RefreshTokenExpirationDays,
            MaxRefreshTokenUses = dto.MaxRefreshTokenUses,
            Values = dto.Values
                .Select(v => new OAuthClientTokenClaimValue
                {
                    Type = v.Type.Trim(),
                    Value = v.Value.Trim()
                })
                .ToList()
        };
}
