using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.OAuthClients;

namespace Lightcode.Registration.Application.Abstractions;

public interface IOAuthClientAppService
{
    Task<ServiceResult<IReadOnlyList<OAuthClientDto>>> ListAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<ServiceResult<OAuthClientDto>> GetByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken = default);

    Task<ServiceResult<OAuthClientDto>> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<ServiceResult<OAuthClientCreatedDto>> CreateAsync(
        string tenantId,
        CreateOAuthClientRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OAuthClientDto>> UpdateByClientIdAsync(
        string tenantId,
        string clientId,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OAuthClientDto>> UpdateByIdAsync(
        string tenantId,
        string id,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeactivateByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeactivateByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
