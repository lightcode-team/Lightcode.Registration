namespace Lightcode.Registration.Domain.Entities;

public sealed class HostedAuthTransaction
{
    public const string CollectionName = "HostedAuthTransactions";

    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string State { get; set; } = default!;
    public string? Nonce { get; set; }
    public string? Scope { get; set; }
    public string? Prompt { get; set; }
    public int? MaxAgeSeconds { get; set; }
    public string CodeChallenge { get; set; } = default!;
    public string CodeChallengeMethod { get; set; } = "S256";
    public string CorrelationId { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class HostedAuthSession
{
    public const string CollectionName = "HostedAuthSessions";

    public string Id { get; set; } = default!;
    public string TransactionId { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string CorrelationId { get; set; } = default!;
    public string Stage { get; set; } = HostedAuthStages.Login;
    public string? SubjectId { get; set; }
    public string? SubjectEmail { get; set; }
    public string? SubjectUsername { get; set; }
    public string? ChallengeId { get; set; }
    public string? VerificationType { get; set; }
    public string? DestinationHint { get; set; }
    public string? MfaMethod { get; set; }
    public DateTime? ChallengeCreatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class SsoSession
{
    public const string CollectionName = "SsoSessions";

    public string Id { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string SubjectId { get; set; } = default!;
    public string? SubjectEmail { get; set; }
    public string? SubjectUsername { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime AuthTimeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? MfaMethod { get; set; }
    public bool TwoFactorSatisfied { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string CorrelationId { get; set; } = default!;
    public string? UserAgentHash { get; set; }
    public string? IpHash { get; set; }
}

public static class HostedAuthStages
{
    public const string Login = "login";
    public const string AwaitingTwoFactor = "awaiting_two_factor";
    public const string Completing = "completing";
    public const string Completed = "completed";
}

public sealed class AuthorizationCodeGrant
{
    public const string CollectionName = "AuthorizationCodeGrants";

    public string Id { get; set; } = default!;
    public string TransactionId { get; set; } = default!;
    public string SessionId { get; set; } = default!;
    public string CodeHash { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string? Nonce { get; set; }
    public string? Scope { get; set; }
    public string CodeChallenge { get; set; } = default!;
    public string CodeChallengeMethod { get; set; } = "S256";
    public string SubjectId { get; set; } = default!;
    public string? MfaMethod { get; set; }
    public string CorrelationId { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}

public sealed class AuthAuditLog
{
    public const string CollectionName = "AuthAuditLogs";

    public string Id { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? SubjectId { get; set; }
    public string? TransactionId { get; set; }
    public string? SessionId { get; set; }
    public string? ChallengeId { get; set; }
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = "info";
    public string? Detail { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}

public static class AuthAuditEventTypes
{
    public const string HostedAuthorizationStarted = "hosted_authorization_started";
    public const string HostedAuthorizationRejected = "hosted_authorization_rejected";
    public const string LoginAttempted = "login_attempted";
    public const string LoginSucceeded = "login_succeeded";
    public const string LoginFailed = "login_failed";
    public const string TwoFactorSent = "two_factor_sent";
    public const string TwoFactorResent = "two_factor_resent";
    public const string TwoFactorConfirmed = "two_factor_confirmed";
    public const string TwoFactorFailed = "two_factor_failed";
    public const string TwoFactorCancelled = "two_factor_cancelled";
    public const string AuthorizationCodeIssued = "authorization_code_issued";
    public const string AuthorizationCodeConsumed = "authorization_code_consumed";
    public const string AuthorizationCodeFailed = "authorization_code_failed";
    public const string PasswordRecoveryRequested = "password_recovery_requested";
    public const string PasswordResetCompleted = "password_reset_completed";
    public const string SsoSessionCreated = "sso_session_created";
    public const string SsoSessionReused = "sso_session_reused";
    public const string SsoSessionRevoked = "sso_session_revoked";
    public const string SsoSessionExpired = "sso_session_expired";
    public const string SsoPromptLogin = "sso_prompt_login";
    public const string SsoPromptNoneFailed = "sso_prompt_none_failed";
    public const string LogoutCompleted = "logout_completed";
}
