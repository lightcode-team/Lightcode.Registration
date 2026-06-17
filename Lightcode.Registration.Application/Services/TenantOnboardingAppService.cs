using System.Net.Mail;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.Application.Contracts.Tenants;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class TenantOnboardingAppService(
    ITenantProvisioner tenantProvisioner,
    IPlatformAdminAppService platformAdminAppService,
    IEmailEnqueuePublisher emailEnqueuePublisher,
    IOptions<MasterOptions> masterOptions,
    IRuntimeEnvironment runtimeEnvironment) : ITenantOnboardingAppService
{
    private const string TenantOnboardingTemplateKey = "tenant-onboarding";

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

        var platformAdmin = await platformAdminAppService.EnsureTenantOwnerAsync(
            adminEmail,
            provision.Tenant.Id,
            sendEmail: false,
            cancellationToken);
        if (!platformAdmin.IsSuccess)
            return ServiceResult<TenantCreatedDto>.Fail(platformAdmin.StatusCode, platformAdmin.Errors);

        await SendTenantOnboardingEmailAsync(
            adminEmail,
            provision,
            platformAdmin.Value!,
            cancellationToken);

        return ServiceResult<TenantCreatedDto>.Ok(
            new TenantCreatedDto(
                provision.Tenant.Id,
                provision.Tenant.Name,
                provision.Tenant.DatabaseName,
                provision.OAuthClientId));
    }

    private async Task SendTenantOnboardingEmailAsync(
        string adminEmail,
        TenantProvisionResult provision,
        InvitePlatformAdminResult ownerInvite,
        CancellationToken cancellationToken)
    {
        var activationUrl = ownerInvite.ActivationUrl;
        var activationToken = ownerInvite.InviteToken;
        var expiresAt = ownerInvite.ExpiresAtUtc == default
            ? string.Empty
            : ownerInvite.ExpiresAtUtc.ToString("O");

        await emailEnqueuePublisher.PublishSendAsync(
            new EmailDispatchQueueMessage(
                provision.Tenant.Id,
                TemplateId: null,
                TemplateKey: TenantOnboardingTemplateKey,
                To: adminEmail,
                Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["tenantId"] = provision.Tenant.Id,
                    ["tenantName"] = provision.Tenant.Name,
                    ["clientId"] = provision.OAuthClientId,
                    ["clientSecret"] = provision.OAuthClientSecretPlaintext,
                    ["activationUrl"] = string.IsNullOrWhiteSpace(activationUrl) ? "Administrador já ativo." : activationUrl,
                    ["activationToken"] = string.IsNullOrWhiteSpace(activationToken) ? "Administrador já ativo." : activationToken,
                    ["expiresAtUtc"] = string.IsNullOrWhiteSpace(expiresAt) ? "Administrador já ativo." : expiresAt
                }),
            cancellationToken);
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
