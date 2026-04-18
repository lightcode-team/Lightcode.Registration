using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Tenants;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITenantOnboardingAppService
{
    Task<ServiceResult<TenantCreatedDto>> CreateTenantAsync(CreateTenantCommand command, CancellationToken cancellationToken = default);
}
