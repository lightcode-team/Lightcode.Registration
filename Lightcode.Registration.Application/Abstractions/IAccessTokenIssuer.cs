using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccessTokenIssuer
{
    IssueTokenResponse CreateAccessToken(string subjectId, string tenantId, TokenIssuanceProfile profile);

    IssueTokenResponse CreatePlatformAdminAccessToken(string adminId, string email);
}
