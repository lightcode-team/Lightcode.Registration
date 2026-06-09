using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Security;

/// <summary>Perfil resolvido para emissão de JWT.</summary>
public sealed class TokenIssuanceProfile
{
    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public int AccessTokenExpirationMinutes { get; init; }

    public int RefreshTokenExpirationDays { get; init; }

    public int MaxRefreshTokenUses { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public string? ClientId { get; init; }

    public string? UserId { get; init; }

    public string? Email { get; init; }

    public string? Username { get; init; }

    public static TokenIssuanceProfile FromOAuthClient(OAuthClient client)
    {
        var config = client.TokenConfig;
        var issuer = ResolveSingle(config, TokenClaimTypes.Issuer);
        var audience = ResolveSingle(config, TokenClaimTypes.Audience);
        if (string.IsNullOrWhiteSpace(audience))
            audience = issuer;

        return new TokenIssuanceProfile
        {
            Issuer = issuer,
            Audience = audience,
            AccessTokenExpirationMinutes = config.AccessTokenExpirationMinutes,
            RefreshTokenExpirationDays = config.RefreshTokenExpirationDays,
            MaxRefreshTokenUses = config.MaxRefreshTokenUses,
            Roles = ResolveMany(config, TokenClaimTypes.Role),
            Scopes = ResolveMany(config, TokenClaimTypes.Scope),
            ClientId = client.ClientId
        };
    }

    public static TokenIssuanceProfile ForPasswordGrant(
        JwtOptions jwt,
        string tenantId,
        IReadOnlyList<string> roles,
        string? userId = null,
        string? email = null,
        string? username = null)
    {
        return new TokenIssuanceProfile
        {
            Issuer = $"{jwt.Issuer}/{tenantId}",
            Audience = $"{jwt.Audience}/{tenantId}",
            AccessTokenExpirationMinutes = jwt.ExpirationMinutes,
            RefreshTokenExpirationDays = jwt.RefreshTokenExpirationDays,
            MaxRefreshTokenUses = jwt.MaxRefreshTokenUses,
            Roles = roles,
            Scopes = [],
            UserId = userId,
            Email = email,
            Username = username
        };
    }

    private static string ResolveSingle(OAuthClientTokenConfiguration config, string type) =>
        config.Values
            .FirstOrDefault(v => string.Equals(v.Type, type, StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim() ?? "";

    private static IReadOnlyList<string> ResolveMany(OAuthClientTokenConfiguration config, string type) =>
        config.Values
            .Where(v => string.Equals(v.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(v => v.Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
