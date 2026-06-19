using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using System.Security.Claims;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccessTokenIssuer
{
    IssueTokenResponse CreateAccessToken(
        string subjectId,
        string tenantId,
        TokenIssuanceProfile profile,
        TenantSigningKeyMaterial signingKey,
        IEnumerable<Claim>? additionalClaims = null);

    IssueTokenResponse CreatePlatformAdminAccessToken(
        string adminId,
        string email,
        IEnumerable<Claim>? additionalClaims = null);
}
