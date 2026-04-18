namespace Lightcode.Registration.Application.Security;

/// <summary>Roles por tenant (campo <c>roles</c> no documento Users; claims <c>role</c> repetidas no JWT).</summary>
public static class UserRoles
{
    public const string Admin = "admin";
    public const string User = "user";

    /// <summary>Normaliza um único valor para <see cref="Admin"/> ou <see cref="User"/>; desconhecido → <see cref="User"/>.</summary>
    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return User;

        return role.Trim().ToLowerInvariant() switch
        {
            Admin => Admin,
            User => User,
            _ => User
        };
    }

    /// <summary>Normaliza uma lista de roles (únicos, ordenados); entradas vazias ou desconhecidas são ignoradas; se ficar vazio, devolve só <see cref="User"/>.</summary>
    public static IReadOnlyList<string> NormalizeMany(IEnumerable<string?>? roles)
    {
        if (roles is null)
            return [User];

        var ordered = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var r in roles)
        {
            var n = Normalize(r);
            ordered.Add(n);
        }

        if (ordered.Count == 0)
            ordered.Add(User);

        return ordered.ToList();
    }

    public static bool HasRole(IReadOnlyList<string> roles, string role) =>
        roles.Contains(Normalize(role), StringComparer.Ordinal);

    public static bool IsAdmin(IReadOnlyList<string> roles) => HasRole(roles, Admin);

    /// <summary>Verifica admin a partir das claims <c>role</c> do JWT (várias entradas).</summary>
    public static bool IsAdminFromClaims(IEnumerable<string>? roleClaims) =>
        IsAdmin(NormalizeMany(roleClaims));
}
