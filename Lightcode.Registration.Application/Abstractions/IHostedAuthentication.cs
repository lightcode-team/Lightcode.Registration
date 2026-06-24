using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IHostedAuthTransactionRepository
{
    Task InsertAsync(HostedAuthTransaction transaction, CancellationToken cancellationToken = default);
    Task<HostedAuthTransaction?> FindActiveAsync(string id, CancellationToken cancellationToken = default);
}

public interface IHostedAuthSessionRepository
{
    Task InsertAsync(HostedAuthSession session, CancellationToken cancellationToken = default);
    Task<HostedAuthSession?> FindActiveByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default);
    Task ReplaceAsync(HostedAuthSession session, CancellationToken cancellationToken = default);
    Task<bool> TryBeginCompletionAsync(string id, string expectedStage, CancellationToken cancellationToken = default);
}

public interface IAuthorizationCodeRepository
{
    Task InsertAsync(AuthorizationCodeGrant grant, CancellationToken cancellationToken = default);
    Task<AuthorizationCodeGrant?> TryConsumeAsync(
        string tenantId,
        string codeHash,
        string clientId,
        string redirectUri,
        string codeChallenge,
        CancellationToken cancellationToken = default);
}

public interface IAuthAuditLogRepository
{
    Task InsertAsync(AuthAuditLog entry, CancellationToken cancellationToken = default);
}

public interface ISsoSessionRepository
{
    Task InsertAsync(SsoSession session, CancellationToken cancellationToken = default);
    Task<SsoSession?> FindActiveAsync(
        string id,
        string tenantId,
        DateTime idleCutoffUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task TouchAsync(string id, DateTime lastSeenAtUtc, CancellationToken cancellationToken = default);
    Task RevokeAsync(string id, DateTime revokedAtUtc, CancellationToken cancellationToken = default);
    Task RevokeBySubjectAsync(
        string tenantId,
        string subjectId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IHostedAuthenticationAppService
{
    Task<ServiceResult<HostedAuthTransaction>> StartAsync(HostedAuthorizationRequest request, CancellationToken cancellationToken = default);
    Task<HostedAuthTransaction?> GetActiveAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<HostedAuthSession?> GetActiveSessionAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<ServiceResult<HostedAuthOperationResult>> LoginAsync(string transactionId, string? username, string? password, CancellationToken cancellationToken = default);
    Task<ServiceResult<HostedAuthOperationResult>> LoginAsync(string transactionId, string? username, string? password, HostedSsoContext? ssoContext, CancellationToken cancellationToken = default);
    Task<ServiceResult<HostedAuthOperationResult>> ConfirmTwoFactorAsync(string transactionId, string? code, CancellationToken cancellationToken = default);
    Task<ServiceResult<HostedAuthOperationResult>> ConfirmTwoFactorAsync(string transactionId, string? code, HostedSsoContext? ssoContext, CancellationToken cancellationToken = default);
    Task<ServiceResult<HostedAuthOperationResult>> CompleteFromSsoAsync(string transactionId, string? ssoSessionId, HostedSsoContext? ssoContext, CancellationToken cancellationToken = default);
    Task<ServiceResult<TwoFactorChallengeDto>> ResendTwoFactorAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<ServiceResult<HostedAuthTransaction>> CancelTwoFactorAsync(string transactionId, CancellationToken cancellationToken = default);
    Task<ServiceResult<HostedLogoutResult>> LogoutAsync(string? tenantId, string? ssoSessionId, string? postLogoutRedirectUri, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthTokenResponse>> ExchangeAuthorizationCodeAsync(TokenRequest request, string tenantId, CancellationToken cancellationToken = default);
}
