namespace Lightcode.Registration.Application.OAuthClients;

public static class OAuthRedirectUriValidator
{
    private static readonly HashSet<string> BlockedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "file", "javascript"
    };

    public static (IReadOnlyList<string> Values, IReadOnlyList<string> Errors) Normalize(
        IEnumerable<string>? redirectUris)
    {
        var values = (redirectUris ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var errors = new List<string>();
        if (values.Count > 20)
            errors.Add("São permitidas no máximo 20 redirect URIs.");

        foreach (var value in values)
        {
            if (value.Length > 2048
                || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || !string.IsNullOrEmpty(uri.Fragment)
                || BlockedSchemes.Contains(uri.Scheme)
                || (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback))
            {
                errors.Add($"Redirect URI inválida: '{value}'. Use HTTPS, HTTP em localhost ou um custom scheme, sem fragmento.");
            }
        }

        return (values, errors);
    }
}
