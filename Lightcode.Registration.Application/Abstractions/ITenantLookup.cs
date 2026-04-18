using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITenantLookup
{
    Task<Tenant?> FindActiveByIdAsync(string tenantId, CancellationToken cancellationToken = default);
}
