using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountTwoFactorAppService(
    ITenantLookup tenantLookup,
    IUserAccountWriter userAccountWriter,
    IAccountJsonSchemaRepository schemaRepository,
    ITwoFactorChallengeService challengeService,
    ITwoFactorSettingsService settingsService,
    IRefreshTokenRepository refreshTokenRepository) : IAccountTwoFactorAppService
{
    public async Task<ServiceResult<TwoFactorBeginResponse>> BeginEnableEmailAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveContextAsync(tenantId, userId, cancellationToken);
        if (!context.IsSuccess)
            return ServiceResult<TwoFactorBeginResponse>.Fail(context.StatusCode, context.Errors);

        if (context.Value!.Mode == AccountAuthTwoFactorModes.Disabled)
            return ServiceResult<TwoFactorBeginResponse>.Fail(403, "2FA não está habilitado para este schema.");

        var challenge = await challengeService.CreateEmailChallengeAsync(
            context.Value.Subject,
            TwoFactorChallengePurposes.EnableTwoFactor,
            cancellationToken);

        return ServiceResult<TwoFactorBeginResponse>.Ok(new TwoFactorBeginResponse(
            challenge.ChallengeId,
            challenge.VerificationType,
            challenge.ExpiresInSeconds,
            challenge.DestinationHint));
    }

    public async Task<ServiceResult<object>> ConfirmEnableEmailAsync(
        string tenantId,
        string userId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var verified = await VerifyForUserAsync(
            tenantId,
            userId,
            request,
            TwoFactorChallengePurposes.EnableTwoFactor,
            cancellationToken);
        if (!verified.IsSuccess)
            return ServiceResult<object>.Fail(verified.StatusCode, verified.Errors);

        await settingsService.SetUserEmailTwoFactorAsync(tenantId, userId, true, cancellationToken);
        await refreshTokenRepository.RevokeBySubjectAsync(tenantId, userId, TokenSubjectTypes.User, cancellationToken);
        return ServiceResult<object>.Ok(new { }, 200, "2FA ativado com sucesso.");
    }

    public async Task<ServiceResult<TwoFactorBeginResponse>> BeginDisableAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var context = await ResolveContextAsync(tenantId, userId, cancellationToken);
        if (!context.IsSuccess)
            return ServiceResult<TwoFactorBeginResponse>.Fail(context.StatusCode, context.Errors);

        var settings = await settingsService.GetUserSettingsAsync(tenantId, userId, cancellationToken);
        if (!settings.Enabled)
            return ServiceResult<TwoFactorBeginResponse>.Fail(409, "2FA já está desativado.");

        var challenge = await challengeService.CreateEmailChallengeAsync(
            context.Value!.Subject,
            TwoFactorChallengePurposes.DisableTwoFactor,
            cancellationToken);

        return ServiceResult<TwoFactorBeginResponse>.Ok(new TwoFactorBeginResponse(
            challenge.ChallengeId,
            challenge.VerificationType,
            challenge.ExpiresInSeconds,
            challenge.DestinationHint));
    }

    public async Task<ServiceResult<object>> ConfirmDisableAsync(
        string tenantId,
        string userId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var verified = await VerifyForUserAsync(
            tenantId,
            userId,
            request,
            TwoFactorChallengePurposes.DisableTwoFactor,
            cancellationToken);
        if (!verified.IsSuccess)
            return ServiceResult<object>.Fail(verified.StatusCode, verified.Errors);

        await settingsService.SetUserEmailTwoFactorAsync(tenantId, userId, false, cancellationToken);
        await refreshTokenRepository.RevokeBySubjectAsync(tenantId, userId, TokenSubjectTypes.User, cancellationToken);
        return ServiceResult<object>.Ok(new { }, 200, "2FA desativado com sucesso.");
    }

    private async Task<ServiceResult<TwoFactorChallenge>> VerifyForUserAsync(
        string tenantId,
        string userId,
        ConfirmTwoFactorRequest request,
        string purpose,
        CancellationToken cancellationToken)
    {
        var verified = await challengeService.VerifyAsync(
            request.ChallengeId ?? string.Empty,
            request.Code ?? string.Empty,
            tenantId,
            TwoFactorSubjectTypes.TenantUser,
            purpose,
            cancellationToken);

        if (!verified.IsSuccess)
            return verified;

        if (!string.Equals(verified.Value!.SubjectId, userId, StringComparison.Ordinal))
            return ServiceResult<TwoFactorChallenge>.Fail(403, "Challenge não pertence ao usuário autenticado.");

        return verified;
    }

    private async Task<ServiceResult<UserTwoFactorContext>> ResolveContextAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
            return ServiceResult<UserTwoFactorContext>.Fail(400, "Tenant e usuário são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<UserTwoFactorContext>.Fail(404, "Tenant não encontrado ou inativo.");

        var json = await userAccountWriter.GetUserDocumentJsonAsync(tenant.Id, userId.Trim(), cancellationToken);
        if (json is null || JsonNode.Parse(json) is not JsonObject obj)
            return ServiceResult<UserTwoFactorContext>.Fail(404, "Conta não encontrada.");

        var status = obj["status"] is JsonValue s && s.TryGetValue<string>(out var sv) ? sv : AccountStatuses.Active;
        if (status is AccountStatuses.PendingConfirmation or AccountStatuses.Expired)
            return ServiceResult<UserTwoFactorContext>.Fail(403, "Conta não está ativa para configurar 2FA.");

        var schemaId = obj[AccountUserFields.SchemaId] is JsonValue sid && sid.TryGetValue<string>(out var schemaValue)
            ? schemaValue
            : null;

        var mode = AccountAuthTwoFactorModes.Disabled;
        if (!string.IsNullOrWhiteSpace(schemaId))
        {
            var schema = await schemaRepository.GetByIdAsync(tenant.Id, schemaId, cancellationToken)
                ?? await schemaRepository.GetByKeyAsync(tenant.Id, schemaId, cancellationToken);
            mode = schema?.GetConfig().Auth?.TwoFactor?.Mode?.Trim().ToLowerInvariant()
                ?? AccountAuthTwoFactorModes.Disabled;
        }

        var email = obj["email"] is JsonValue e && e.TryGetValue<string>(out var ev) ? ev : string.Empty;
        var username = obj["username"] is JsonValue u && u.TryGetValue<string>(out var uv) ? uv : email;

        return ServiceResult<UserTwoFactorContext>.Ok(new UserTwoFactorContext(
            mode,
            new TwoFactorChallengeSubject(
                TwoFactorSubjectTypes.TenantUser,
                userId.Trim(),
                tenant.Id,
                email,
                username)));
    }

    private sealed record UserTwoFactorContext(
        string Mode,
        TwoFactorChallengeSubject Subject);
}
