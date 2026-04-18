using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountJsonSchemaRepository
{
    Task<IReadOnlyList<AccountJsonSchema>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<AccountJsonSchema?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<AccountJsonSchema?> GetByKeyAsync(string tenantId, string key, CancellationToken cancellationToken = default);

    Task<AccountJsonSchema?> GetDefaultAsync(string tenantId, CancellationToken cancellationToken = default);

    Task InsertAsync(AccountJsonSchema entity, CancellationToken cancellationToken = default);

    Task ReplaceAsync(AccountJsonSchema entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task ClearDefaultFlagForTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
