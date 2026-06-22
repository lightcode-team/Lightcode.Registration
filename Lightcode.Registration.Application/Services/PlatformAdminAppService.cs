using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.Application.Emails;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.TwoFactor;
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
    ITwoFactorSettingsService twoFactorSettingsService,
    ITwoFactorChallengeService twoFactorChallengeService,
    IOptions<MasterOptions> masterOptions,
    IOptions<JwtOptions> jwtOptions,
    IOptions<RegistrationOptions> registrationOptions,
    IRuntimeEnvironment runtimeEnvironment) : IPlatformAdminAppService
{
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

    public async Task<ServiceResult<AuthTokenResponse>> IssueTokenAsync(
        PlatformAdminTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<AuthTokenResponse>.Fail(400, "Email e password são obrigatórios.");

        var admin = await repository.FindAdminByEmailAsync(email, cancellationToken);
        if (admin is null
            || admin.Status != PlatformAdminStatuses.Active
            || string.IsNullOrWhiteSpace(admin.PasswordHash)
            || !passwordHasher.Verify(request.Password, admin.PasswordHash))
            return ServiceResult<AuthTokenResponse>.Fail(401, "Credenciais inválidas.");

        var settings = await twoFactorSettingsService.GetPlatformAdminSettingsAsync(admin.Id, cancellationToken);
        if (settings.Enabled && settings.EmailEnabled)
        {
            var challenge = await twoFactorChallengeService.CreateEmailChallengeAsync(
                new TwoFactorChallengeSubject(
                    TwoFactorSubjectTypes.PlatformAdmin,
                    admin.Id,
                    null,
                    admin.Email,
                    admin.Email),
                TwoFactorChallengePurposes.Login,
                cancellationToken);

            return ServiceResult<AuthTokenResponse>.Ok(AuthTokenResponse.TwoFactorRequired(challenge));
        }

        var token = accessTokenIssuer.CreatePlatformAdminAccessToken(admin.Id, admin.Email);
        return ServiceResult<AuthTokenResponse>.Ok(AuthTokenResponse.Issued(token));
    }

    public async Task<ServiceResult<AuthTokenResponse>> ConfirmTwoFactorAsync(
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var verify = await twoFactorChallengeService.VerifyAsync(
            request.ChallengeId ?? string.Empty,
            request.Code ?? string.Empty,
            tenantId: null,
            TwoFactorSubjectTypes.PlatformAdmin,
            TwoFactorChallengePurposes.Login,
            cancellationToken);

        if (!verify.IsSuccess)
            return ServiceResult<AuthTokenResponse>.Fail(verify.StatusCode, verify.Errors);

        var challenge = verify.Value!;
        var admin = await RequireActiveAdminAsync(challenge.SubjectId, cancellationToken);
        if (admin is null)
            return ServiceResult<AuthTokenResponse>.Fail(401, "Administrador inválido ou inativo.");

        var token = accessTokenIssuer.CreatePlatformAdminAccessToken(
            admin.Id,
            admin.Email,
            CreateMfaClaims(challenge.Method));

        return ServiceResult<AuthTokenResponse>.Ok(AuthTokenResponse.Issued(token));
    }

    public async Task<ServiceResult<TwoFactorBeginResponse>> BeginEnableEmailTwoFactorAsync(
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var admin = await RequireActiveAdminAsync(adminId, cancellationToken);
        if (admin is null)
            return ServiceResult<TwoFactorBeginResponse>.Fail(401, "Administrador inválido ou inativo.");

        var settings = await twoFactorSettingsService.GetPlatformAdminSettingsAsync(admin.Id, cancellationToken);
        if (settings.Enabled && settings.EmailEnabled)
            return ServiceResult<TwoFactorBeginResponse>.Fail(409, "2FA já está ativado.");

        var challenge = await CreatePlatformAdminChallengeAsync(
            admin,
            TwoFactorChallengePurposes.EnableTwoFactor,
            cancellationToken);

        return ServiceResult<TwoFactorBeginResponse>.Ok(ToBeginResponse(challenge));
    }

    public async Task<ServiceResult<bool>> ConfirmEnableEmailTwoFactorAsync(
        string adminId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var verify = await VerifyPlatformAdminChallengeAsync(
            adminId,
            request,
            TwoFactorChallengePurposes.EnableTwoFactor,
            cancellationToken);
        if (!verify.IsSuccess)
            return ServiceResult<bool>.Fail(verify.StatusCode, verify.Errors);

        await twoFactorSettingsService.SetPlatformAdminEmailTwoFactorAsync(adminId.Trim(), true, cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<TwoFactorBeginResponse>> BeginDisableTwoFactorAsync(
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var admin = await RequireActiveAdminAsync(adminId, cancellationToken);
        if (admin is null)
            return ServiceResult<TwoFactorBeginResponse>.Fail(401, "Administrador inválido ou inativo.");

        var settings = await twoFactorSettingsService.GetPlatformAdminSettingsAsync(admin.Id, cancellationToken);
        if (!settings.Enabled)
            return ServiceResult<TwoFactorBeginResponse>.Fail(409, "2FA já está desativado.");

        var challenge = await CreatePlatformAdminChallengeAsync(
            admin,
            TwoFactorChallengePurposes.DisableTwoFactor,
            cancellationToken);

        return ServiceResult<TwoFactorBeginResponse>.Ok(ToBeginResponse(challenge));
    }

    public async Task<ServiceResult<bool>> ConfirmDisableTwoFactorAsync(
        string adminId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var verify = await VerifyPlatformAdminChallengeAsync(
            adminId,
            request,
            TwoFactorChallengePurposes.DisableTwoFactor,
            cancellationToken);
        if (!verify.IsSuccess)
            return ServiceResult<bool>.Fail(verify.StatusCode, verify.Errors);

        await twoFactorSettingsService.SetPlatformAdminEmailTwoFactorAsync(adminId.Trim(), false, cancellationToken);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<PlatformAdminTwoFactorStatusResult>> GetTwoFactorStatusAsync(
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var admin = await RequireActiveAdminAsync(adminId, cancellationToken);
        if (admin is null)
            return ServiceResult<PlatformAdminTwoFactorStatusResult>.Fail(401, "Administrador inválido ou inativo.");

        var settings = await twoFactorSettingsService.GetPlatformAdminSettingsAsync(admin.Id, cancellationToken);

        return ServiceResult<PlatformAdminTwoFactorStatusResult>.Ok(
            new PlatformAdminTwoFactorStatusResult(
                settings.Enabled,
                settings.EmailEnabled,
                settings.PreferredMethod));
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
                PlatformEmailTemplates.TenantId,
                TemplateId: null,
                TemplateKey: PlatformEmailTemplates.PlatformAdminInvite,
                To: email,
                Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["tenantName"] = tenantName,
                    ["activationToken"] = token,
                    ["activationUrl"] = activationUrl ?? token,
                    ["expiresAtUtc"] = expiresAt.ToString("O")
                },
                SystemEmail: true),
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

    private async Task<TwoFactorChallengeDto> CreatePlatformAdminChallengeAsync(
        PlatformAdmin admin,
        string purpose,
        CancellationToken cancellationToken) =>
        await twoFactorChallengeService.CreateEmailChallengeAsync(
            new TwoFactorChallengeSubject(
                TwoFactorSubjectTypes.PlatformAdmin,
                admin.Id,
                null,
                admin.Email,
                admin.Email),
            purpose,
            cancellationToken);

    private async Task<ServiceResult<bool>> VerifyPlatformAdminChallengeAsync(
        string adminId,
        ConfirmTwoFactorRequest request,
        string purpose,
        CancellationToken cancellationToken)
    {
        var admin = await RequireActiveAdminAsync(adminId, cancellationToken);
        if (admin is null)
            return ServiceResult<bool>.Fail(401, "Administrador inválido ou inativo.");

        var verify = await twoFactorChallengeService.VerifyAsync(
            request.ChallengeId ?? string.Empty,
            request.Code ?? string.Empty,
            tenantId: null,
            TwoFactorSubjectTypes.PlatformAdmin,
            purpose,
            cancellationToken);
        if (!verify.IsSuccess)
            return ServiceResult<bool>.Fail(verify.StatusCode, verify.Errors);

        return string.Equals(verify.Value!.SubjectId, admin.Id, StringComparison.Ordinal)
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Fail(403, "Challenge não pertence ao administrador autenticado.");
    }

    private static TwoFactorBeginResponse ToBeginResponse(TwoFactorChallengeDto challenge) =>
        new(
            challenge.ChallengeId,
            challenge.VerificationType,
            challenge.ExpiresInSeconds,
            challenge.DestinationHint);

    private static IReadOnlyList<Claim> CreateMfaClaims(string method) =>
    [
        new("amr", "pwd"),
        new("amr", "mfa"),
        new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        new("mfa_method", method)
    ];

    private string? BuildActivationUrl(string token)
    {
        var baseUrl = registrationOptions.Value.PublicApiBaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : $"{baseUrl}/completar-dados?token={Uri.EscapeDataString(token)}";
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
