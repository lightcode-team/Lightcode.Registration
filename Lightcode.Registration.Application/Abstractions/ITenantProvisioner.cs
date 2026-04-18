using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITenantProvisioner
{
    Task<Tenant> ProvisionAsync(string name, CancellationToken cancellationToken = default);
}
