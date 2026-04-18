using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccessTokenIssuer
{
    IssueTokenResponse CreateToken(string userId, string tenantId);
}
