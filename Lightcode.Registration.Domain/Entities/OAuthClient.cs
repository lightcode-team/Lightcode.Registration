namespace Lightcode.Registration.Domain.Entities;

/// <summary>Cliente OAuth2 (grant <c>client_credentials</c>) na base de dados do tenant.</summary>
public sealed class OAuthClient
{
    public const string CollectionName = "OAuthClients";

    public string Id { get; set; } = default!;

    /// <summary>Identificador público do cliente (enviado no pedido de token).</summary>
    public string ClientId { get; set; } = default!;

    /// <summary>Hash PBKDF2 do segredo do cliente.</summary>
    public string ClientSecretHash { get; set; } = default!;

    public string? DisplayName { get; set; }

    public OAuthClientTokenConfiguration TokenConfig { get; set; } = new();

    public bool Active { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Configuração de emissão de tokens para um cliente OAuth.</summary>
public sealed class OAuthClientTokenConfiguration
{
    public int AccessTokenExpirationMinutes { get; set; } = 120;

    public int RefreshTokenExpirationDays { get; set; } = 30;

    public int MaxRefreshTokenUses { get; set; } = 5;

    public List<OAuthClientTokenClaimValue> Values { get; set; } = [];
}

public sealed class OAuthClientTokenClaimValue
{
    /// <summary>Ex.: <c>iss</c>, <c>aud</c>, <c>scope</c>, <c>role</c>.</summary>
    public string Type { get; set; } = "";

    public string Value { get; set; } = "";
}
