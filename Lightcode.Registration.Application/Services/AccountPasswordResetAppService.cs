using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Email;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountPasswordResetAppService(
    ITenantLookup tenantLookup,
    IUserAccountWriter userAccountWriter,
    ISecureTokenGenerator tokenGenerator,
    IPasswordHasher passwordHasher,
    IEmailEnqueuePublisher emailEnqueuePublisher,
    IOptions<RegistrationOptions> registrationOptions) : IAccountPasswordResetAppService
{
    private const string SuccessMessage =
        "Se o email ou utilizador existir, receberá um link para redefinir a senha.";

    public async Task<ServiceResult<object>> ForgotPasswordAsync(
        string tenantId,
        string? email,
        string? username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<object>.Fail(400, "TenantId é obrigatório.");

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(username))
            return ServiceResult<object>.Fail(400, "Email ou username é obrigatório.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<object>.Ok(new { }, 200, SuccessMessage);

        var resolvedEmail = await userAccountWriter.TryGetActiveUserEmailAsync(
            tenant.Id,
            email,
            username,
            cancellationToken);

        if (resolvedEmail is null)
            return ServiceResult<object>.Ok(new { }, 200, SuccessMessage);

        var plainToken = tokenGenerator.GeneratePasswordResetToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(AccountPasswordResetFields.ExpirationMinutes);
        var stored = await userAccountWriter.TrySetPasswordResetTokenAsync(
            tenant.Id,
            resolvedEmail,
            passwordHasher.Hash(plainToken),
            expiresAt,
            cancellationToken);

        if (stored)
        {
            await emailEnqueuePublisher.PublishSendAsync(
                new EmailDispatchQueueMessage(
                    tenant.Id,
                    TemplateId: null,
                    TemplateKey: AccountPasswordResetFields.TemplateKey,
                    To: resolvedEmail,
                    Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["resetLink"] = BuildResetPasswordLink(tenant.Id, resolvedEmail, plainToken)
                    }),
                cancellationToken);
        }

        return ServiceResult<object>.Ok(new { }, 200, SuccessMessage);
    }

    public async Task<ServiceResult<object>> ResetPasswordAsync(
        string tenantId,
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<object>.Fail(400, "TenantId é obrigatório.");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return ServiceResult<object>.Fail(400, "Email e token são obrigatórios.");

        if (string.IsNullOrWhiteSpace(newPassword))
            return ServiceResult<object>.Fail(400, "Nova senha é obrigatória.");

        if (newPassword.Length < 8)
            return ServiceResult<object>.Fail(400, "A senha deve ter pelo menos 8 caracteres.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<object>.Fail(404, "Tenant não encontrado ou inativo.");

        var reset = await userAccountWriter.TryResetPasswordAsync(
            tenant.Id,
            email.Trim().ToLowerInvariant(),
            token.Trim(),
            passwordHasher.Hash(newPassword),
            cancellationToken);

        if (!reset)
            return ServiceResult<object>.Fail(400, "Link inválido, expirado ou conta inexistente.");

        return ServiceResult<object>.Ok(new { }, 200, "Senha redefinida com sucesso.");
    }

    private string BuildResetPasswordLink(string tenantId, string email, string token)
    {
        var baseUrl = registrationOptions.Value.PublicApiBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://localhost:5012";

        var encodedTenantId = Uri.EscapeDataString(tenantId);
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(token);
        return $"{baseUrl}/reset-password?token={encodedToken}&tenantId={encodedTenantId}&email={encodedEmail}";
    }
}
