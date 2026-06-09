using System.Security.Claims;

namespace Lightcode.Registration.AspNetCore.Security;

internal static class JwtClaimReader
{
    public static string? GetIssuer(ClaimsPrincipal principal) =>
        principal.FindFirst("iss")?.Value;

    public static IReadOnlyList<string> GetAudiences(ClaimsPrincipal principal) =>
        principal.FindAll("aud")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
