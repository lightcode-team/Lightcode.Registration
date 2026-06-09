namespace Lightcode.Registration.Domain.Entities;

/// <summary>Refresh token opaco persistido na base de dados do tenant.</summary>
public sealed class RefreshToken
{
    public const string CollectionName = "RefreshTokens";

    public string Id { get; set; } = default!;

    /// <summary>Hash SHA-256 do valor opaco do refresh token.</summary>
    public string TokenHash { get; set; } = default!;

    /// <summary>Id do utilizador ou do cliente OAuth.</summary>
    public string SubjectId { get; set; } = default!;

    /// <summary><c>user</c> ou <c>client</c>.</summary>
    public string SubjectType { get; set; } = default!;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public IReadOnlyList<string> Scopes { get; set; } = [];

    public DateTime ExpiresAtUtc { get; set; }

    public int UseCount { get; set; }

    public int MaxUses { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
}
