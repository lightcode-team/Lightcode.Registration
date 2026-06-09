using System.Net.Mail;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Contracts.Tenants;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class TenantOnboardingAppService(
    ITenantProvisioner tenantProvisioner,
    IEmailEnqueuePublisher emailEnqueuePublisher,
    IOptions<MasterOptions> masterOptions,
    IRuntimeEnvironment runtimeEnvironment) : ITenantOnboardingAppService
{
    private const string ClientCredentialsTemplateKey = "client-credentials-secret";

    public async Task<ServiceResult<TenantCreatedDto>> CreateTenantAsync(
        CreateTenantCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return ServiceResult<TenantCreatedDto>.Fail(400, "Nome é obrigatório.");

        var adminEmail = command.AdminEmail?.Trim();
        if (string.IsNullOrWhiteSpace(adminEmail))
            return ServiceResult<TenantCreatedDto>.Fail(400, "adminEmail é obrigatório.");

        if (!IsValidEmail(adminEmail))
            return ServiceResult<TenantCreatedDto>.Fail(400, "adminEmail inválido.");

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

        var provision = await tenantProvisioner.ProvisionAsync(
            new TenantProvisionRequest(command.Name.Trim(), adminEmail),
            cancellationToken);

        await emailEnqueuePublisher.PublishSendAsync(
            new EmailDispatchQueueMessage(
                provision.Tenant.Id,
                TemplateId: null,
                TemplateKey: ClientCredentialsTemplateKey,
                To: adminEmail,
                Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["tenantName"] = provision.Tenant.Name,
                    ["tenantId"] = provision.Tenant.Id,
                    ["clientId"] = provision.OAuthClientId,
                    ["clientSecret"] = provision.OAuthClientSecretPlaintext
                }),
            cancellationToken);

        return ServiceResult<TenantCreatedDto>.Ok(
            new TenantCreatedDto(
                provision.Tenant.Id,
                provision.Tenant.Name,
                provision.Tenant.DatabaseName,
                provision.OAuthClientId));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return email.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
