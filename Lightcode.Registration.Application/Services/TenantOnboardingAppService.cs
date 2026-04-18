using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Tenants;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class TenantOnboardingAppService(
    ITenantProvisioner tenantProvisioner,
    IOptions<MasterOptions> masterOptions,
    IRuntimeEnvironment runtimeEnvironment) : ITenantOnboardingAppService
{
    public async Task<ServiceResult<TenantCreatedDto>> CreateTenantAsync(
        CreateTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return ServiceResult<TenantCreatedDto>.Fail(400, "Nome é obrigatório.");

        var expected = masterOptions.Value.ProvisioningApiKey;
        if (!string.IsNullOrEmpty(expected))
        {
            if (!string.Equals(command.ProvisioningKeyFromRequest, expected, StringComparison.Ordinal))
                return ServiceResult<TenantCreatedDto>.Fail(401, "Chave de provisionamento inválida ou ausente.");
        }
        else if (!runtimeEnvironment.IsDevelopment)
        {
            return ServiceResult<TenantCreatedDto>.Fail(
                403,
                "Defina Master:ProvisioningApiKey para permitir criação de tenants em produção.");
        }

        var tenant = await tenantProvisioner.ProvisionAsync(command.Name.Trim(), cancellationToken);
        return ServiceResult<TenantCreatedDto>.Ok(
            new TenantCreatedDto(tenant.Id, tenant.Name, tenant.DatabaseName));
    }
}
