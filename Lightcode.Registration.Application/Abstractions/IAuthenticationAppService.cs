using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAuthenticationAppService
{
    /// <param name="tenantId">Tenant obtido do cabeçalho HTTP quando o pedido não traz JWT.</param>
    Task<ServiceResult<IssueTokenResponse>> IssueTokenAsync(
        IssueTokenRequest request,
        string tenantId,
        CancellationToken cancellationToken = default);
}
