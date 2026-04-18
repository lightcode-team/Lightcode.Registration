using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Services;

public sealed class AuthenticationAppService(
    ITenantLookup tenantLookup,
    IAccessTokenIssuer accessTokenIssuer) : IAuthenticationAppService
{
    public async Task<ServiceResult<IssueTokenResponse>> IssueTokenAsync(
        IssueTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.TenantId))
            return ServiceResult<IssueTokenResponse>.Fail(400, "UserId e TenantId são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(request.TenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<IssueTokenResponse>.Fail(401, "Tenant não encontrado ou inativo.");

        var token = accessTokenIssuer.CreateToken(request.UserId.Trim(), tenant.Id);
        return ServiceResult<IssueTokenResponse>.Ok(token);
    }
}
