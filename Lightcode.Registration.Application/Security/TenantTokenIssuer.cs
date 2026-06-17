using Lightcode.Registration.Application.Configuration;

namespace Lightcode.Registration.Application.Security;

public static class TenantTokenIssuer
{
    public static string Build(RegistrationOptions registration, JwtOptions jwt, string tenantId)
    {
        var baseIssuer = !string.IsNullOrWhiteSpace(registration.PublicApiBaseUrl)
            ? registration.PublicApiBaseUrl
            : jwt.Issuer;

        return $"{baseIssuer.Trim().TrimEnd('/')}/tenants/{tenantId}";
    }
}
