using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.OAuthClients;

public static class OAuthScopeValidator
{
    private static readonly string[] BasicScopes = ["openid", "profile", "email"];

    public static (IReadOnlyList<string> Values, IReadOnlyList<string> Errors) Normalize(IReadOnlyList<string>? scopes)
    {
        if (scopes is null || scopes.Count == 0)
            return ([], []);

        var errors = new List<string>();
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in scopes.SelectMany(x => (x ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            var scope = raw.Trim();
            if (scope.Length is < 1 or > 128 || scope.Any(char.IsWhiteSpace))
            {
                errors.Add("Escopo OAuth invalido.");
                continue;
            }

            if (!scope.All(IsAllowedScopeCharacter))
            {
                errors.Add("Escopo OAuth contem caracteres invalidos.");
                continue;
            }

            if (seen.Add(scope))
                values.Add(scope);
        }

        return (values, errors.Distinct(StringComparer.Ordinal).ToList());
    }

    public static (string? Scope, IReadOnlyList<string> Errors) ValidateRequestedScope(
        OAuthClient client,
        string? requestedScope)
    {
        var requested = Normalize(string.IsNullOrWhiteSpace(requestedScope) ? null : [requestedScope]);
        if (requested.Errors.Count > 0)
            return (null, requested.Errors);

        if (requested.Values.Count == 0)
            return (null, []);

        var allowed = ResolveAllowedScopes(client);
        var invalid = requested.Values
            .Where(scope => !allowed.Contains(scope))
            .ToList();

        return invalid.Count == 0
            ? (string.Join(' ', requested.Values), [])
            : (null, ["Escopo OAuth nao autorizado para o cliente."]);
    }

    public static IReadOnlySet<string> ResolveAllowedScopes(OAuthClient client)
    {
        var allowed = new HashSet<string>(BasicScopes, StringComparer.Ordinal);

        foreach (var scope in Normalize(client.AllowedScopes).Values)
            allowed.Add(scope);

        foreach (var scope in client.TokenConfig.Values
                     .Where(x => string.Equals(x.Type, "scope", StringComparison.OrdinalIgnoreCase))
                     .Select(x => x.Value))
        {
            foreach (var normalized in Normalize([scope]).Values)
                allowed.Add(normalized);
        }

        return allowed;
    }

    private static bool IsAllowedScopeCharacter(char value) =>
        value is >= '\u0021' and <= '\u007e' && value != '"' && value != '\\';
}
