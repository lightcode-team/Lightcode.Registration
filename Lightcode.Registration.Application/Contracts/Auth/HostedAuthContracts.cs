namespace Lightcode.Registration.Application.Contracts.Auth;

public sealed record HostedAuthorizationRequest(
    string? ResponseType,
    string? TenantId,
    string? ClientId,
    string? RedirectUri,
    string? State,
    string? Nonce,
    string? Scope,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? Prompt = null,
    string? MaxAge = null);

public sealed record HostedSsoContext(
    string? ExistingSessionId,
    string? UserAgentHash,
    string? IpHash);

public sealed record HostedPasswordAuthenticationResult(
    string SubjectId,
    string Email,
    string Username,
    TwoFactorChallengeDto? Challenge,
    string? MfaMethod = null,
    IReadOnlyList<string>? Roles = null)
{
    public bool RequiresTwoFactor => Challenge is not null;
}

public sealed record HostedAuthOperationResult(
    bool Completed,
    string? RedirectUrl,
    TwoFactorChallengeDto? Challenge,
    string? SsoSessionId = null,
    DateTime? SsoSessionExpiresAtUtc = null);

public sealed record HostedLogoutResult(string? RedirectUrl);
