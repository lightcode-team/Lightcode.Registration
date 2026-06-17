using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class PlatformAdminAppService(
    IPlatformAdminRepository repository,
    ITenantLookup tenantLookup,
    IPasswordHasher passwordHasher,
    ISecureTokenGenerator tokenGenerator,
    IAccessTokenIssuer accessTokenIssuer,
    ITenantSigningKeyResolver tenantSigningKeyResolver,
    IEmailEnqueuePublisher emailEnqueuePublisher,
    IOptions<MasterOptions> masterOptions,
    IOptions<JwtOptions> jwtOptions,
    IOptions<RegistrationOptions> registrationOptions,
    IRuntimeEnvironment runtimeEnvironment) : IPlatformAdminAppService
{
    private const string InviteTemplateKey = "platform-admin-invite";
    private const int InviteExpirationDays = 7;

    public async Task<ServiceResult<InvitePlatformAdminResult>> InviteAsync(
        InvitePlatformAdminCommand command,
        CancellationToken cancellationToken = default)
    {
        var provisioningResult = ValidateProvisioningKey(command.ProvisioningKeyFromRequest);
        if (!provisioningResult.IsSuccess)
            return ServiceResult<InvitePlatformAdminResult>.Fail(provisioningResult.StatusCode, provisioningResult.Errors);

        return await CreateInviteAsync(
            command.Email,
            command.TenantIds ?? [],
            sendEmail: true,
            skipInviteIfActive: false,
            cancellationToken);
    }

    public async Task<ServiceResult<InvitePlatformAdminResult>> EnsureTenantOwnerAsync(
        string email,
        string tenantId,
        bool sendEmail = true,
        CancellationToken cancellationToken = default) =>
        await CreateInviteAsync(
            email,
            [tenantId],
            sendEmail,
            skipInviteIfActive: true,
            cancellationToken);

    public async Task<ServiceResult<ActivatePlatformAdminResult>> ActivateAsync(
        ActivatePlatformAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return ServiceResult<ActivatePlatformAdminResult>.Fail(400, "Token Ã© obrigatÃ³rio.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return ServiceResult<ActivatePlatformAdminResult>.Fail(400, "Password deve ter pelo menos 8 caracteres.");

        var tokenHash = HashToken(request.Token.Trim());
        var invite = await repository.FindPendingInviteByTokenHashAsync(tokenHash, cancellationToken);
        if (invite is null)
            return ServiceResult<ActivatePlatformAdminResult>.Fail(401, "Convite invÃ¡lido.");

        if (invite.ExpiresAtUtc <= DateTime.UtcNow)
            return ServiceResult<ActivatePlatformAdminResult>.Fail(401, "Convite expirado.");

        var admin = await repository.GetAdminByIdAsync(invite.AdminId, cancellationToken);
        if (admin is null)
            return ServiceResult<ActivatePlatformAdminResult>.Fail(404, "Administrador nÃ£o encontrado.");

        admin.PasswordHash = passwordHasher.Hash(request.Password);
        admin.Status = PlatformAdminStatuses.Active;
        admin.UpdatedAtUtc = DateTime.UtcNow;

        await repository.ReplaceAdminAsync(admin, cancellationToken);
        await repository.MarkInviteUsedAsync(invite.Id, cancellationToken);

        return ServiceResult<ActivatePlatformAdminResult>.Ok(
            new ActivatePlatformAdminResult(admin.Id, admin.Email));
    }

    public async Task<ServiceResult<IssueTokenResponse>> IssueTokenAsync(
        PlatformAdminTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<IssueTokenResponse>.Fail(400, "Email e password sÃ£o obrigatÃ³rios.");

        var admin = await repository.FindAdminByEmailAsync(email, cancellationToken);
        if (admin is null
            || admin.Status != PlatformAdminStatuses.Active
            || string.IsNullOrWhiteSpace(admin.PasswordHash)
            || !passwordHasher.Verify(request.Password, admin.PasswordHash))
            return ServiceResult<IssueTokenResponse>.Fail(401, "Credenciais invÃ¡lidas.");

        var token = accessTokenIssuer.CreatePlatformAdminAccessToken(admin.Id, admin.Email);
        return ServiceResult<IssueTokenResponse>.Ok(token);
    }

    public async Task<ServiceResult<IReadOnlyList<PlatformTenantDto>>> ListTenantsAsync(
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var admin = await RequireActiveAdminAsync(adminId, cancellationToken);
        if (admin is null)
            return ServiceResult<IReadOnlyList<PlatformTenantDto>>.Fail(401, "Administrador invÃ¡lido ou inativo.");

        var tenants = await repository.ListActiveTenantsForAdminAsync(admin.Id, cancellationToken);
        return ServiceResult<IReadOnlyList<PlatformTenantDto>>.Ok(tenants);
    }

    public async Task<ServiceResult<PlatformTenantTokenResult>> IssueTenantTokenAsync(
        string adminId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<PlatformTenantTokenResult>.Fail(400, "TenantId Ã© obrigatÃ³rio.");

        var admin = await RequireActiveAdminAsync(adminId, cancellationToken);
        if (admin is null)
            return ServiceResult<PlatformTenantTokenResult>.Fail(401, "Administrador invÃ¡lido ou inativo.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<PlatformTenantTokenResult>.Fail(404, "Tenant nÃ£o encontrado ou inativo.");

        var link = await repository.FindActiveTenantLinkAsync(admin.Id, tenant.Id, cancellationToken);
        if (link is null)
            return ServiceResult<PlatformTenantTokenResult>.Fail(403, "Administrador nÃ£o vinculado a este tenant.");

        var profile = TokenIssuanceProfile.ForPlatformAdminTenant(
            jwtOptions.Value,
            registrationOptions.Value,
            tenant.Id,
            admin.Id,
            admin.Email);

        var signingKey = await tenantSigningKeyResolver.ResolveSigningKeyAsync(tenant.Id, cancellationToken);
        var token = accessTokenIssuer.CreateAccessToken(admin.Id, tenant.Id, profile, signingKey);
        return ServiceResult<PlatformTenantTokenResult>.Ok(new PlatformTenantTokenResult(tenant.Id, token));
    }

    private async Task<ServiceResult<InvitePlatformAdminResult>> CreateInviteAsync(
        string? rawEmail,
        IReadOnlyList<string> tenantIds,
        bool sendEmail,
        bool skipInviteIfActive,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(rawEmail);
        if (email is null)
            return ServiceResult<InvitePlatformAdminResult>.Fail(400, "Email invÃ¡lido.");

        var now = DateTime.UtcNow;
        var admin = await repository.FindAdminByEmailAsync(email, cancellationToken);
        if (admin is null)
        {
            admin = new PlatformAdmin
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = email,
                Status = PlatformAdminStatuses.PendingActivation,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await repository.InsertAdminAsync(admin, cancellationToken);
        }

        var linkedTenants = new List<(string Id, string Name)>();
        foreach (var tenantId in tenantIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal))
        {
            var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
                return ServiceResult<InvitePlatformAdminResult>.Fail(404, $"Tenant '{tenantId}' nÃ£o encontrado ou inativo.");

            await repository.UpsertTenantLinkAsync(
                admin.Id,
                tenant.Id,
                PlatformAdminTenantRoles.Owner,
                cancellationToken);
            linkedTenants.Add((tenant.Id, tenant.Name));
        }

        if (skipInviteIfActive && admin.Status == PlatformAdminStatuses.Active)
        {
            return ServiceResult<InvitePlatformAdminResult>.Ok(
                new InvitePlatformAdminResult(admin.Id, email, string.Empty, null, now),
                message: "Administrador vinculado ao tenant.");
        }

        var plainToken = tokenGenerator.GeneratePasswordResetToken();
        var expiresAt = now.AddDays(InviteExpirationDays);
        var invite = new PlatformAdminInvite
        {
            Id = Guid.NewGuid().ToString("N"),
            AdminId = admin.Id,
            Email = email,
            TokenHash = HashToken(plainToken),
            Status = PlatformAdminInviteStatuses.Pending,
            ExpiresAtUtc = expiresAt,
            CreatedAtUtc = now
        };

        await repository.InsertInviteAsync(invite, cancellationToken);

        var activationUrl = BuildActivationUrl(plainToken);
        if (sendEmail && linkedTenants.Count > 0)
        {
            var firstTenant = linkedTenants[0];
            await TrySendInviteEmailAsync(
                firstTenant.Id,
                firstTenant.Name,
                email,
                plainToken,
                activationUrl,
                expiresAt,
                cancellationToken);
        }

        return ServiceResult<InvitePlatformAdminResult>.Ok(
            new InvitePlatformAdminResult(admin.Id, email, plainToken, activationUrl, expiresAt),
            201,
            "Convite criado.");
    }

    private async Task TrySendInviteEmailAsync(
        string tenantId,
        string tenantName,
        string email,
        string token,
        string? activationUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        await emailEnqueuePublisher.PublishSendAsync(
            new EmailDispatchQueueMessage(
                tenantId,
                TemplateId: null,
                TemplateKey: InviteTemplateKey,
                To: email,
                Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["tenantName"] = tenantName,
                    ["activationToken"] = token,
                    ["activationUrl"] = activationUrl ?? token,
                    ["expiresAtUtc"] = expiresAt.ToString("O")
                }),
            cancellationToken);
    }

    private ServiceResult<bool> ValidateProvisioningKey(string? provisioningKeyFromRequest)
    {
        var expected = masterOptions.Value.ProvisioningApiKey;
        if (!string.IsNullOrEmpty(expected))
        {
            return string.Equals(provisioningKeyFromRequest, expected, StringComparison.Ordinal)
                ? ServiceResult<bool>.Ok(true)
                : ServiceResult<bool>.Fail(401, "Chave de provisionamento invÃ¡lida ou ausente.");
        }

        return runtimeEnvironment.IsDevelopment
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Fail(403, "Defina Master:ProvisioningApiKey para convidar administradores em produÃ§Ã£o.");
    }

    private async Task<PlatformAdmin?> RequireActiveAdminAsync(string adminId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminId))
            return null;

        var admin = await repository.GetAdminByIdAsync(adminId.Trim(), cancellationToken);
        return admin?.Status == PlatformAdminStatuses.Active ? admin : null;
    }

    private string? BuildActivationUrl(string token)
    {
        var baseUrl = registrationOptions.Value.PublicApiBaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl}/platform-admins/activate?token={Uri.EscapeDataString(token)}";
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();
        try
        {
            _ = new MailAddress(normalized);
            return normalized.Contains('@', StringComparison.Ordinal) ? normalized : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
