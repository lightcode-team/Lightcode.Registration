using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IOAuthClientRepository
{
    Task<IReadOnlyList<OAuthClient>> ListAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<OAuthClient?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<OAuthClient?> FindByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken = default);

    Task InsertAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default);

    Task ReplaceAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
