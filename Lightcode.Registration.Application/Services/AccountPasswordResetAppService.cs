using System.Security.Cryptography;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountPasswordResetAppService(
    ITenantLookup tenantLookup,
    IUserAccountWriter userAccountWriter,
    ISecureTokenGenerator tokenGenerator,
    IPasswordHasher passwordHasher,
    IEmailEnqueuePublisher emailEnqueuePublisher,
    IRefreshTokenRepository refreshTokenRepository,
    IAuthAuditLogRepository auditLogRepository,
    IOptions<RegistrationOptions> registrationOptions,
    ILogger<AccountPasswordResetAppService> logger) : IAccountPasswordResetAppService
{
    private const string SuccessMessage =
        "Se o email ou utilizador existir, receberá um link para redefinir a senha.";

    public async Task<ServiceResult<object>> ForgotPasswordAsync(
        string tenantId,
        string? email,
        string? username,
        CancellationToken cancellationToken = default)
        => await ForgotPasswordAsync(tenantId, email, username, continuationId: null, cancellationToken);

    public async Task<ServiceResult<object>> ForgotPasswordAsync(
        string tenantId,
        string? email,
        string? username,
        string? continuationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<object>.Fail(400, "TenantId é obrigatório.");

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(username))
            return ServiceResult<object>.Fail(400, "Email ou username é obrigatório.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
        {
            logger.LogInformation(
                "Pedido de recuperacao de senha ignorado: tenant inexistente ou inativo. TenantId={TenantId}",
                tenantId.Trim());
            return ServiceResult<object>.Ok(new { }, 200, SuccessMessage);
        }

        await AuditAsync(
            AuthAuditEventTypes.PasswordRecoveryRequested,
            tenant.Id,
            subjectId: null,
            detail: "requested",
            new Dictionary<string, string>
            {
                ["identifier_hash"] = HashForAudit(email ?? username)
            },
            cancellationToken);

        var resolvedEmail = await userAccountWriter.TryGetActiveUserEmailAsync(
            tenant.Id,
            email,
            username,
            cancellationToken);

        if (resolvedEmail is null)
        {
            logger.LogInformation(
                "Pedido de recuperacao de senha nao enfileirado: nenhum usuario ativo encontrado. TenantId={TenantId} IdentifierHash={IdentifierHash}",
                tenant.Id,
                HashForAudit(email ?? username));
            return ServiceResult<object>.Ok(new { }, 200, SuccessMessage);
        }

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
            var messageId = await emailEnqueuePublisher.PublishSendAsync(
                new EmailDispatchQueueMessage(
                    tenant.Id,
                    TemplateId: null,
                    TemplateKey: AccountPasswordResetFields.TemplateKey,
                    To: resolvedEmail,
                    Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["resetLink"] = BuildResetPasswordLink(tenant.Id, resolvedEmail, plainToken, continuationId)
                    }),
                cancellationToken);

            logger.LogInformation(
                "Email de recuperacao de senha enfileirado TenantId={TenantId} TemplateKey={TemplateKey} MessageId={MessageId}",
                tenant.Id,
                AccountPasswordResetFields.TemplateKey,
                messageId);
        }
        else
        {
            logger.LogInformation(
                "Pedido de recuperacao de senha nao enfileirado: token nao foi gravado para usuario ativo. TenantId={TenantId}",
                tenant.Id);
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

        var userId = await userAccountWriter.TryGetActiveUserIdByEmailAsync(
            tenant.Id,
            email.Trim().ToLowerInvariant(),
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await refreshTokenRepository.RevokeBySubjectAsync(tenant.Id, userId, TokenSubjectTypes.User, cancellationToken);
            await AuditAsync(
                AuthAuditEventTypes.PasswordResetCompleted,
                tenant.Id,
                userId,
                detail: "refresh_tokens_revoked",
                metadata: null,
                cancellationToken);
        }

        return ServiceResult<object>.Ok(new { }, 200, "Senha redefinida com sucesso.");
    }

    private string BuildResetPasswordLink(string tenantId, string email, string token, string? continuationId)
    {
        var baseUrl = registrationOptions.Value.PublicApiBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://localhost:5012";

        var encodedTenantId = Uri.EscapeDataString(tenantId);
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(token);
        var link = $"{baseUrl}/reset-password?token={encodedToken}&tenantId={encodedTenantId}&email={encodedEmail}";
        return string.IsNullOrWhiteSpace(continuationId)
            ? link
            : $"{link}&transactionId={Uri.EscapeDataString(continuationId.Trim())}";
    }

    private async Task AuditAsync(
        string eventType,
        string tenantId,
        string? subjectId,
        string? detail,
        Dictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        await auditLogRepository.InsertAsync(
            new AuthAuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                TenantId = tenantId,
                SubjectId = subjectId,
                CorrelationId = Guid.NewGuid().ToString("N"),
                Status = "info",
                Detail = detail,
                Metadata = metadata ?? [],
                CreatedAtUtc = DateTime.UtcNow
            },
            cancellationToken);
    }

    private static string HashForAudit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }
}
