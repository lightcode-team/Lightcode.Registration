using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Services;

public sealed class AuthenticationAppService(
    ITenantLookup tenantLookup,
    IUserCredentialValidator credentialValidator,
    IAccessTokenIssuer accessTokenIssuer) : IAuthenticationAppService
{
    public async Task<ServiceResult<IssueTokenResponse>> IssueTokenAsync(
        IssueTokenRequest request,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<IssueTokenResponse>.Fail(400, "O cabeçalho do tenant (X-Tenant-Id) é obrigatório.");

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<IssueTokenResponse>.Fail(400, "Username e password são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<IssueTokenResponse>.Fail(401, "Tenant não encontrado ou inativo.");

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var credentials = await credentialValidator.ValidateAsync(tenant.Id, normalizedUsername, request.Password, cancellationToken);
        if (credentials is null)
            return ServiceResult<IssueTokenResponse>.Fail(401, "Credenciais inválidas.");

        var token = accessTokenIssuer.CreateToken(credentials.UserId, tenant.Id, credentials.Roles);
        return ServiceResult<IssueTokenResponse>.Ok(token);
    }
}
