using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.OAuthClients;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Services;

public sealed class HostedAuthenticationAppService(
    IAuthenticationAppService authenticationAppService,
    IOAuthClientRepository oauthClientRepository,
    IHostedAuthTransactionRepository transactionRepository,
    IHostedAuthSessionRepository sessionRepository,
    IAuthorizationCodeRepository authorizationCodeRepository,
    IAuthAuditLogRepository auditLogRepository,
    ISsoSessionRepository ssoSessionRepository,
    IUserAccountWriter userAccountWriter,
    ITwoFactorChallengeService twoFactorChallengeService,
    ITwoFactorChallengeRepository twoFactorChallengeRepository,
    ISecureTokenGenerator tokenGenerator) : IHostedAuthenticationAppService
{
    private const string PublicInvalidAuthorization = "Solicitacao de autorizacao invalida.";
    private const string PublicInvalidLogin = "Credenciais invalidas ou fluxo expirado.";
    private const string PublicInvalidTwoFactor = "Codigo 2FA invalido ou expirado.";
    private const string PublicInvalidCode = "Authorization code invalido ou expirado.";
    private const string PublicLoginRequired = "login_required";
    private static readonly TimeSpan TransactionLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SsoSessionLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan SsoIdleTimeout = TimeSpan.FromMinutes(30);

    public async Task<ServiceResult<HostedAuthTransaction>> StartAsync(
        HostedAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var prompt = NormalizeOptional(request.Prompt);
        if (prompt is not null
            && !string.Equals(prompt, "login", StringComparison.Ordinal)
            && !string.Equals(prompt, "none", StringComparison.Ordinal))
        {
            await AuditAsync(AuthAuditEventTypes.HostedAuthorizationRejected, "failure", correlationId, detail: "invalid_prompt", cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthTransaction>.Fail(400, PublicInvalidAuthorization);
        }

        int? maxAgeSeconds = null;
        if (!string.IsNullOrWhiteSpace(request.MaxAge)
            && (!int.TryParse(request.MaxAge.Trim(), out var parsedMaxAge) || parsedMaxAge < 0))
        {
            await AuditAsync(AuthAuditEventTypes.HostedAuthorizationRejected, "failure", correlationId, detail: "invalid_max_age", cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthTransaction>.Fail(400, PublicInvalidAuthorization);
        }

        if (!string.IsNullOrWhiteSpace(request.MaxAge))
            maxAgeSeconds = int.Parse(request.MaxAge.Trim());

        if (!string.Equals(request.ResponseType?.Trim(), "code", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.TenantId)
            || string.IsNullOrWhiteSpace(request.ClientId)
            || string.IsNullOrWhiteSpace(request.RedirectUri)
            || string.IsNullOrWhiteSpace(request.State)
            || request.State.Length > 1024
            || request.Nonce?.Length > 1024
            || !OAuthPkce.IsValidChallenge(request.CodeChallenge, request.CodeChallengeMethod))
        {
            await AuditAsync(AuthAuditEventTypes.HostedAuthorizationRejected, "failure", correlationId, detail: "invalid_request", cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthTransaction>.Fail(400, PublicInvalidAuthorization);
        }

        var tenantId = request.TenantId.Trim();
        var clientId = request.ClientId.Trim();
        var redirectUri = request.RedirectUri.Trim();
        var client = await oauthClientRepository.FindByClientIdAsync(tenantId, clientId, cancellationToken);
        if (client is null
            || !client.Active
            || !client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
        {
            await AuditAsync(
                AuthAuditEventTypes.HostedAuthorizationRejected,
                "failure",
                correlationId,
                tenantId,
                clientId,
                detail: "client_or_redirect_not_authorized",
                cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthTransaction>.Fail(400, PublicInvalidAuthorization);
        }

        var scope = OAuthScopeValidator.ValidateRequestedScope(client, request.Scope);
        if (scope.Errors.Count > 0)
        {
            await AuditAsync(
                AuthAuditEventTypes.HostedAuthorizationRejected,
                "failure",
                correlationId,
                tenantId,
                clientId,
                detail: "invalid_scope",
                cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthTransaction>.Fail(400, PublicInvalidAuthorization);
        }

        var now = DateTime.UtcNow;
        var transaction = new HostedAuthTransaction
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            ClientId = clientId,
            RedirectUri = redirectUri,
            State = request.State.Trim(),
            Nonce = NormalizeOptional(request.Nonce),
            Scope = scope.Scope,
            Prompt = prompt,
            MaxAgeSeconds = maxAgeSeconds,
            CodeChallenge = request.CodeChallenge!.Trim(),
            CodeChallengeMethod = "S256",
            CorrelationId = correlationId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(TransactionLifetime)
        };

        var session = new HostedAuthSession
        {
            Id = Guid.NewGuid().ToString("N"),
            TransactionId = transaction.Id,
            TenantId = tenantId,
            ClientId = clientId,
            CorrelationId = correlationId,
            Stage = HostedAuthStages.Login,
            CreatedAtUtc = now,
            ExpiresAtUtc = transaction.ExpiresAtUtc
        };

        await transactionRepository.InsertAsync(transaction, cancellationToken);
        await sessionRepository.InsertAsync(session, cancellationToken);
        await AuditAsync(
            AuthAuditEventTypes.HostedAuthorizationStarted,
            "success",
            correlationId,
            tenantId,
            clientId,
            transaction.Id,
            session.Id,
            metadata: new Dictionary<string, string>
            {
                ["scope"] = transaction.Scope ?? string.Empty,
                ["nonce_present"] = string.IsNullOrWhiteSpace(transaction.Nonce) ? "false" : "true",
                ["prompt"] = transaction.Prompt ?? string.Empty,
                ["max_age"] = transaction.MaxAgeSeconds?.ToString() ?? string.Empty
            },
            cancellationToken: cancellationToken);

        if (string.Equals(transaction.Prompt, "login", StringComparison.Ordinal))
        {
            await AuditAsync(
                AuthAuditEventTypes.SsoPromptLogin,
                "info",
                correlationId,
                tenantId,
                clientId,
                transaction.Id,
                session.Id,
                cancellationToken: cancellationToken);
        }

        Trace.TraceInformation(
            "Hosted authorization started. tenant={0} client={1} transaction={2} session={3} correlation={4}",
            tenantId,
            clientId,
            transaction.Id,
            session.Id,
            correlationId);

        return ServiceResult<HostedAuthTransaction>.Ok(transaction);
    }

    public Task<HostedAuthTransaction?> GetActiveAsync(
        string transactionId,
        CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(transactionId)
            ? Task.FromResult<HostedAuthTransaction?>(null)
            : transactionRepository.FindActiveAsync(transactionId.Trim(), cancellationToken);

    public Task<HostedAuthSession?> GetActiveSessionAsync(
        string transactionId,
        CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(transactionId)
            ? Task.FromResult<HostedAuthSession?>(null)
            : sessionRepository.FindActiveByTransactionIdAsync(transactionId.Trim(), cancellationToken);

    public async Task<ServiceResult<HostedAuthOperationResult>> LoginAsync(
        string transactionId,
        string? username,
        string? password,
        CancellationToken cancellationToken = default)
        => await LoginAsync(transactionId, username, password, ssoContext: null, cancellationToken);

    public async Task<ServiceResult<HostedAuthOperationResult>> LoginAsync(
        string transactionId,
        string? username,
        string? password,
        HostedSsoContext? ssoContext,
        CancellationToken cancellationToken = default)
    {
        var pair = await GetActivePairAsync(transactionId, HostedAuthStages.Login, cancellationToken);
        if (pair is null)
            return ServiceResult<HostedAuthOperationResult>.Fail(400, PublicInvalidLogin);

        var (transaction, session) = pair.Value;
        await AuditAsync(
            AuthAuditEventTypes.LoginAttempted,
            "info",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            metadata: new Dictionary<string, string> { ["username_hash"] = HashForAudit(username) },
            cancellationToken: cancellationToken);

        var result = await authenticationAppService.BeginHostedPasswordAuthenticationAsync(
            username,
            password,
            transaction.TenantId,
            cancellationToken);
        if (!result.IsSuccess)
        {
            await AuditAsync(
                AuthAuditEventTypes.LoginFailed,
                "failure",
                transaction.CorrelationId,
                transaction.TenantId,
                transaction.ClientId,
                transaction.Id,
                session.Id,
                detail: $"status:{result.StatusCode}",
                metadata: new Dictionary<string, string> { ["username_hash"] = HashForAudit(username) },
                cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Fail(401, PublicInvalidLogin);
        }

        var identity = result.Value!;
        session.SubjectId = identity.SubjectId;
        session.SubjectEmail = identity.Email;
        session.SubjectUsername = identity.Username;

        await AuditAsync(
            AuthAuditEventTypes.LoginSucceeded,
            "success",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            identity.SubjectId,
            cancellationToken: cancellationToken);

        if (identity.Challenge is { } challenge)
        {
            session.Stage = HostedAuthStages.AwaitingTwoFactor;
            ApplyChallenge(session, challenge);
            await sessionRepository.ReplaceAsync(session, cancellationToken);
            await AuditAsync(
                AuthAuditEventTypes.TwoFactorSent,
                "success",
                transaction.CorrelationId,
                transaction.TenantId,
                transaction.ClientId,
                transaction.Id,
                session.Id,
                identity.SubjectId,
                challenge.ChallengeId,
                cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Ok(new(false, null, challenge));
        }

        return await CompleteAsync(transaction, session, mfaMethod: null, ssoContext, cancellationToken);
    }

    public async Task<ServiceResult<HostedAuthOperationResult>> ConfirmTwoFactorAsync(
        string transactionId,
        string? code,
        CancellationToken cancellationToken = default)
        => await ConfirmTwoFactorAsync(transactionId, code, ssoContext: null, cancellationToken);

    public async Task<ServiceResult<HostedAuthOperationResult>> ConfirmTwoFactorAsync(
        string transactionId,
        string? code,
        HostedSsoContext? ssoContext,
        CancellationToken cancellationToken = default)
    {
        var pair = await GetActivePairAsync(transactionId, HostedAuthStages.AwaitingTwoFactor, cancellationToken);
        if (pair is null || string.IsNullOrWhiteSpace(pair.Value.Session.ChallengeId))
            return ServiceResult<HostedAuthOperationResult>.Fail(400, PublicInvalidTwoFactor);

        var (transaction, session) = pair.Value;
        var verified = await authenticationAppService.ConfirmHostedTwoFactorAsync(
            new ConfirmTwoFactorRequest(session.ChallengeId, code),
            transaction.TenantId,
            cancellationToken);
        if (!verified.IsSuccess)
        {
            await AuditAsync(
                AuthAuditEventTypes.TwoFactorFailed,
                "failure",
                transaction.CorrelationId,
                transaction.TenantId,
                transaction.ClientId,
                transaction.Id,
                session.Id,
                session.SubjectId,
                session.ChallengeId,
                detail: $"status:{verified.StatusCode}",
                cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Fail(401, PublicInvalidTwoFactor);
        }

        if (!string.Equals(session.SubjectId, verified.Value!.SubjectId, StringComparison.Ordinal))
        {
            await AuditAsync(
                AuthAuditEventTypes.TwoFactorFailed,
                "failure",
                transaction.CorrelationId,
                transaction.TenantId,
                transaction.ClientId,
                transaction.Id,
                session.Id,
                session.SubjectId,
                session.ChallengeId,
                detail: "subject_mismatch",
                cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Fail(403, PublicInvalidTwoFactor);
        }

        await AuditAsync(
            AuthAuditEventTypes.TwoFactorConfirmed,
            "success",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            session.SubjectId,
            session.ChallengeId,
            cancellationToken: cancellationToken);
        return await CompleteAsync(transaction, session, verified.Value.MfaMethod, ssoContext, cancellationToken);
    }

    public async Task<ServiceResult<HostedAuthOperationResult>> CompleteFromSsoAsync(
        string transactionId,
        string? ssoSessionId,
        HostedSsoContext? ssoContext,
        CancellationToken cancellationToken = default)
    {
        var pair = await GetActivePairAsync(transactionId, HostedAuthStages.Login, cancellationToken);
        if (pair is null)
            return ServiceResult<HostedAuthOperationResult>.Fail(400, PublicInvalidLogin);

        var (transaction, session) = pair.Value;
        if (string.Equals(transaction.Prompt, "login", StringComparison.Ordinal))
            return ServiceResult<HostedAuthOperationResult>.Fail(412, PublicLoginRequired);

        if (string.IsNullOrWhiteSpace(ssoSessionId))
        {
            await AuditPromptNoneIfNeededAsync(transaction, session, "missing_session", cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Fail(401, PublicLoginRequired);
        }

        var now = DateTime.UtcNow;
        var ssoSession = await ssoSessionRepository.FindActiveAsync(
            ssoSessionId.Trim(),
            transaction.TenantId,
            now.Subtract(SsoIdleTimeout),
            now,
            cancellationToken);
        if (ssoSession is null)
        {
            await AuditPromptNoneIfNeededAsync(transaction, session, "invalid_or_expired_session", cancellationToken);
            await AuditAsync(
                AuthAuditEventTypes.SsoSessionExpired,
                "failure",
                transaction.CorrelationId,
                transaction.TenantId,
                transaction.ClientId,
                transaction.Id,
                session.Id,
                detail: "invalid_or_expired",
                cancellationToken: cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Fail(401, PublicLoginRequired);
        }

        if (transaction.MaxAgeSeconds is { } maxAge
            && ssoSession.AuthTimeUtc.AddSeconds(maxAge) < now)
        {
            await AuditPromptNoneIfNeededAsync(transaction, session, "max_age_exceeded", cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Fail(401, PublicLoginRequired);
        }

        var status = await userAccountWriter.GetUserStatusAsync(
            transaction.TenantId,
            ssoSession.SubjectId,
            cancellationToken);
        if (!string.Equals(status, AccountStatuses.Active, StringComparison.Ordinal))
        {
            await ssoSessionRepository.RevokeAsync(ssoSession.Id, now, cancellationToken);
            await AuditPromptNoneIfNeededAsync(transaction, session, "user_not_active", cancellationToken);
            return ServiceResult<HostedAuthOperationResult>.Fail(401, PublicLoginRequired);
        }

        await ssoSessionRepository.TouchAsync(ssoSession.Id, now, cancellationToken);
        session.SubjectId = ssoSession.SubjectId;
        session.SubjectEmail = ssoSession.SubjectEmail;
        session.SubjectUsername = ssoSession.SubjectUsername;
        session.MfaMethod = ssoSession.MfaMethod;

        await AuditAsync(
            AuthAuditEventTypes.SsoSessionReused,
            "success",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            ssoSession.SubjectId,
            metadata: new Dictionary<string, string>
            {
                ["sso_session_id"] = ssoSession.Id,
                ["two_factor_satisfied"] = ssoSession.TwoFactorSatisfied ? "true" : "false",
                ["user_agent_match"] = string.Equals(ssoSession.UserAgentHash, ssoContext?.UserAgentHash, StringComparison.Ordinal) ? "true" : "false",
                ["ip_match"] = string.Equals(ssoSession.IpHash, ssoContext?.IpHash, StringComparison.Ordinal) ? "true" : "false"
            },
            cancellationToken: cancellationToken);

        return await CompleteAsync(transaction, session, ssoSession.MfaMethod, ssoContext, cancellationToken, reuseSsoSession: ssoSession);
    }

    public async Task<ServiceResult<TwoFactorChallengeDto>> ResendTwoFactorAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var pair = await GetActivePairAsync(transactionId, HostedAuthStages.AwaitingTwoFactor, cancellationToken);
        if (pair is null
            || string.IsNullOrWhiteSpace(pair.Value.Session.SubjectId)
            || string.IsNullOrWhiteSpace(pair.Value.Session.SubjectEmail))
        {
            return ServiceResult<TwoFactorChallengeDto>.Fail(400, "Fluxo 2FA invalido ou expirado.");
        }

        var (transaction, session) = pair.Value;
        if (session.ChallengeCreatedAtUtc is { } createdAt
            && createdAt.AddSeconds(30) > DateTime.UtcNow)
        {
            return ServiceResult<TwoFactorChallengeDto>.Fail(429, "Aguarde 30 segundos antes de solicitar um novo codigo.");
        }

        var challenge = await twoFactorChallengeService.CreateEmailChallengeAsync(
            new TwoFactorChallengeSubject(
                TwoFactorSubjectTypes.TenantUser,
                session.SubjectId,
                transaction.TenantId,
                session.SubjectEmail,
                session.SubjectUsername ?? session.SubjectEmail),
            TwoFactorChallengePurposes.Login,
            cancellationToken);

        ApplyChallenge(session, challenge);
        await sessionRepository.ReplaceAsync(session, cancellationToken);
        await AuditAsync(
            AuthAuditEventTypes.TwoFactorResent,
            "success",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            session.SubjectId,
            challenge.ChallengeId,
            cancellationToken: cancellationToken);
        return ServiceResult<TwoFactorChallengeDto>.Ok(challenge);
    }

    public async Task<ServiceResult<HostedAuthTransaction>> CancelTwoFactorAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetActiveAsync(transactionId, cancellationToken);
        var session = await GetActiveSessionAsync(transactionId, cancellationToken);
        if (transaction is null || session is null)
            return ServiceResult<HostedAuthTransaction>.Fail(400, "Fluxo 2FA invalido ou expirado.");

        if (!string.IsNullOrWhiteSpace(session.SubjectId))
        {
            await twoFactorChallengeRepository.InvalidatePendingAsync(
                transaction.TenantId,
                TwoFactorSubjectTypes.TenantUser,
                session.SubjectId,
                TwoFactorChallengePurposes.Login,
                TwoFactorMethods.EmailCode,
                cancellationToken);
        }

        await AuditAsync(
            AuthAuditEventTypes.TwoFactorCancelled,
            "success",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            session.SubjectId,
            session.ChallengeId,
            cancellationToken: cancellationToken);

        session.Stage = HostedAuthStages.Login;
        session.SubjectId = null;
        session.SubjectEmail = null;
        session.SubjectUsername = null;
        session.ChallengeId = null;
        session.VerificationType = null;
        session.DestinationHint = null;
        session.MfaMethod = null;
        session.ChallengeCreatedAtUtc = null;
        await sessionRepository.ReplaceAsync(session, cancellationToken);
        return ServiceResult<HostedAuthTransaction>.Ok(transaction);
    }

    public async Task<ServiceResult<AuthTokenResponse>> ExchangeAuthorizationCodeAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.ClientId)
            || string.IsNullOrWhiteSpace(request.RedirectUri)
            || !OAuthPkce.IsValidVerifier(request.CodeVerifier))
        {
            return ServiceResult<AuthTokenResponse>.Fail(400, "Code, client_id, redirect_uri e code_verifier sao obrigatorios.");
        }

        var clientId = request.ClientId.Trim();
        var redirectUri = request.RedirectUri.Trim();
        var client = await oauthClientRepository.FindByClientIdAsync(tenantId, clientId, cancellationToken);
        if (client is null || !client.Active || !client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
        {
            await AuditAsync(
                AuthAuditEventTypes.AuthorizationCodeFailed,
                "failure",
                Guid.NewGuid().ToString("N"),
                tenantId,
                clientId,
                detail: "client_or_redirect_not_authorized",
                cancellationToken: cancellationToken);
            return ServiceResult<AuthTokenResponse>.Fail(401, PublicInvalidCode);
        }

        var grant = await authorizationCodeRepository.TryConsumeAsync(
            tenantId,
            OAuthPkce.HashOpaqueToken(request.Code.Trim()),
            clientId,
            redirectUri,
            OAuthPkce.CreateChallenge(request.CodeVerifier!),
            cancellationToken);
        if (grant is null)
        {
            await AuditAsync(
                AuthAuditEventTypes.AuthorizationCodeFailed,
                "failure",
                Guid.NewGuid().ToString("N"),
                tenantId,
                clientId,
                detail: "code_not_found_or_already_consumed",
                cancellationToken: cancellationToken);
            return ServiceResult<AuthTokenResponse>.Fail(401, PublicInvalidCode);
        }

        await AuditAsync(
            AuthAuditEventTypes.AuthorizationCodeConsumed,
            "success",
            grant.CorrelationId,
            grant.TenantId,
            grant.ClientId,
            grant.TransactionId,
            grant.SessionId,
            grant.SubjectId,
            cancellationToken: cancellationToken);

        Trace.TraceInformation(
            "Authorization code consumed. tenant={0} client={1} subject={2} correlation={3}",
            grant.TenantId,
            grant.ClientId,
            grant.SubjectId,
            grant.CorrelationId);

        return await authenticationAppService.IssueHostedIdentityTokenAsync(
            grant.SubjectId,
            grant.MfaMethod,
            tenantId,
            cancellationToken);
    }

    private async Task<(HostedAuthTransaction Transaction, HostedAuthSession Session)?> GetActivePairAsync(
        string transactionId,
        string expectedStage,
        CancellationToken cancellationToken)
    {
        var transaction = await GetActiveAsync(transactionId, cancellationToken);
        var session = await GetActiveSessionAsync(transactionId, cancellationToken);
        if (transaction is null || session is null || session.Stage != expectedStage)
            return null;

        return (transaction, session);
    }

    private async Task<ServiceResult<HostedAuthOperationResult>> CompleteAsync(
        HostedAuthTransaction transaction,
        HostedAuthSession session,
        string? mfaMethod,
        HostedSsoContext? ssoContext,
        CancellationToken cancellationToken,
        SsoSession? reuseSsoSession = null)
    {
        if (string.IsNullOrWhiteSpace(session.SubjectId))
            return ServiceResult<HostedAuthOperationResult>.Fail(401, "Identidade autenticada invalida.");

        var expectedStage = session.Stage;
        if (!await sessionRepository.TryBeginCompletionAsync(session.Id, expectedStage, cancellationToken))
            return ServiceResult<HostedAuthOperationResult>.Fail(409, "Fluxo de login ja concluido ou em processamento.");

        var plainCode = tokenGenerator.GenerateAuthorizationCode();
        var now = DateTime.UtcNow;
        var ssoSession = reuseSsoSession ?? await CreateSsoSessionAsync(
            transaction,
            session,
            mfaMethod,
            ssoContext,
            now,
            cancellationToken);

        await authorizationCodeRepository.InsertAsync(
            new AuthorizationCodeGrant
            {
                Id = Guid.NewGuid().ToString("N"),
                TransactionId = transaction.Id,
                SessionId = session.Id,
                CodeHash = OAuthPkce.HashOpaqueToken(plainCode),
                TenantId = transaction.TenantId,
                ClientId = transaction.ClientId,
                RedirectUri = transaction.RedirectUri,
                Nonce = transaction.Nonce,
                Scope = transaction.Scope,
                CodeChallenge = transaction.CodeChallenge,
                CodeChallengeMethod = transaction.CodeChallengeMethod,
                SubjectId = session.SubjectId,
                MfaMethod = mfaMethod,
                CorrelationId = transaction.CorrelationId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(AuthorizationCodeLifetime)
            },
            cancellationToken);

        session.Stage = HostedAuthStages.Completed;
        session.MfaMethod = mfaMethod;
        await sessionRepository.ReplaceAsync(session, cancellationToken);
        await AuditAsync(
            AuthAuditEventTypes.AuthorizationCodeIssued,
            "success",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            session.SubjectId,
            metadata: new Dictionary<string, string>
            {
                ["code_hash_prefix"] = OAuthPkce.HashOpaqueToken(plainCode)[..12],
                ["scope"] = transaction.Scope ?? string.Empty,
                ["nonce_present"] = string.IsNullOrWhiteSpace(transaction.Nonce) ? "false" : "true"
            },
            cancellationToken: cancellationToken);

        var separator = transaction.RedirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var redirectUrl = $"{transaction.RedirectUri}{separator}code={Uri.EscapeDataString(plainCode)}&state={Uri.EscapeDataString(transaction.State)}";
        return ServiceResult<HostedAuthOperationResult>.Ok(new(true, redirectUrl, null, ssoSession?.Id, ssoSession?.ExpiresAtUtc));
    }

    public async Task<ServiceResult<HostedLogoutResult>> LogoutAsync(
        string? tenantId,
        string? ssoSessionId,
        string? postLogoutRedirectUri,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var normalizedTenantId = NormalizeOptional(tenantId);
        if (!string.IsNullOrWhiteSpace(ssoSessionId))
            await ssoSessionRepository.RevokeAsync(ssoSessionId.Trim(), DateTime.UtcNow, cancellationToken);

        string? redirectUrl = null;
        if (!string.IsNullOrWhiteSpace(normalizedTenantId) && !string.IsNullOrWhiteSpace(postLogoutRedirectUri))
        {
            var clients = await oauthClientRepository.ListAsync(normalizedTenantId, cancellationToken);
            var requestedRedirect = postLogoutRedirectUri.Trim();
            if (clients.Any(x => x.Active && x.PostLogoutRedirectUris.Contains(requestedRedirect, StringComparer.Ordinal)))
                redirectUrl = requestedRedirect;
        }

        await AuditAsync(
            AuthAuditEventTypes.LogoutCompleted,
            "success",
            correlationId,
            normalizedTenantId,
            detail: string.IsNullOrWhiteSpace(redirectUrl) ? "no_redirect" : "redirect",
            cancellationToken: cancellationToken);

        return ServiceResult<HostedLogoutResult>.Ok(new HostedLogoutResult(redirectUrl));
    }

    private async Task<SsoSession?> CreateSsoSessionAsync(
        HostedAuthTransaction transaction,
        HostedAuthSession session,
        string? mfaMethod,
        HostedSsoContext? ssoContext,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.SubjectId))
            return null;

        if (!string.IsNullOrWhiteSpace(ssoContext?.ExistingSessionId))
            await ssoSessionRepository.RevokeAsync(ssoContext.ExistingSessionId.Trim(), now, cancellationToken);

        var ssoSession = new SsoSession
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = transaction.TenantId,
            SubjectId = session.SubjectId,
            SubjectEmail = session.SubjectEmail,
            SubjectUsername = session.SubjectUsername,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            AuthTimeUtc = now,
            ExpiresAtUtc = now.Add(SsoSessionLifetime),
            MfaMethod = mfaMethod,
            TwoFactorSatisfied = !string.IsNullOrWhiteSpace(mfaMethod),
            CorrelationId = transaction.CorrelationId,
            UserAgentHash = ssoContext?.UserAgentHash,
            IpHash = ssoContext?.IpHash
        };

        await ssoSessionRepository.InsertAsync(ssoSession, cancellationToken);
        await AuditAsync(
            AuthAuditEventTypes.SsoSessionCreated,
            "success",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            session.SubjectId,
            metadata: new Dictionary<string, string>
            {
                ["sso_session_id"] = ssoSession.Id,
                ["two_factor_satisfied"] = ssoSession.TwoFactorSatisfied ? "true" : "false"
            },
            cancellationToken: cancellationToken);

        return ssoSession;
    }

    private async Task AuditPromptNoneIfNeededAsync(
        HostedAuthTransaction transaction,
        HostedAuthSession session,
        string detail,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(transaction.Prompt, "none", StringComparison.Ordinal))
            return;

        await AuditAsync(
            AuthAuditEventTypes.SsoPromptNoneFailed,
            "failure",
            transaction.CorrelationId,
            transaction.TenantId,
            transaction.ClientId,
            transaction.Id,
            session.Id,
            detail: detail,
            cancellationToken: cancellationToken);
    }

    private async Task AuditAsync(
        string eventType,
        string status,
        string correlationId,
        string? tenantId = null,
        string? clientId = null,
        string? transactionId = null,
        string? sessionId = null,
        string? subjectId = null,
        string? challengeId = null,
        string? detail = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await auditLogRepository.InsertAsync(
            new AuthAuditLog
            {
                Id = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                TenantId = tenantId,
                ClientId = clientId,
                SubjectId = subjectId,
                TransactionId = transactionId,
                SessionId = sessionId,
                ChallengeId = challengeId,
                CorrelationId = correlationId,
                Status = status,
                Detail = detail,
                Metadata = metadata ?? [],
                CreatedAtUtc = DateTime.UtcNow
            },
            cancellationToken);
    }

    private static void ApplyChallenge(HostedAuthSession session, TwoFactorChallengeDto challenge)
    {
        session.ChallengeId = challenge.ChallengeId;
        session.VerificationType = challenge.VerificationType;
        session.DestinationHint = challenge.DestinationHint;
        session.ChallengeCreatedAtUtc = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string HashForAudit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }
}
