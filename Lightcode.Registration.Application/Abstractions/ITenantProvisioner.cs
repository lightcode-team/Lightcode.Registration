using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public sealed record TenantProvisionRequest(string Name, string AdminEmail);

public sealed record TenantProvisionResult(
    Tenant Tenant,
    string OAuthClientId,
    string OAuthClientSecretPlaintext);

public interface ITenantProvisioner
{
    Task<TenantProvisionResult> ProvisionAsync(TenantProvisionRequest request, CancellationToken cancellationToken = default);
}
