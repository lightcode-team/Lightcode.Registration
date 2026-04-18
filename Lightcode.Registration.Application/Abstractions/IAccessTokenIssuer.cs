using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccessTokenIssuer
{
    /// <param name="roles">Lista de roles (ex.: <c>admin</c>, <c>user</c>); cada valor gera uma claim <c>role</c> no JWT.</param>
    IssueTokenResponse CreateToken(string userId, string tenantId, IReadOnlyList<string> roles);
}
