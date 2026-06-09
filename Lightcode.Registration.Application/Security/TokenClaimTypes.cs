namespace Lightcode.Registration.Application.Security;

public static class TokenClaimTypes
{
    public const string Issuer = "iss";
    public const string Audience = "aud";
    public const string Scope = "scope";
    public const string Role = "role";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Issuer, Audience, Scope, Role
    };
}
