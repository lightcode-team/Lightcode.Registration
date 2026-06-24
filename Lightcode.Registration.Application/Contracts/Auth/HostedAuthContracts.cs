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
    string? CodeChallengeMethod);

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
    TwoFactorChallengeDto? Challenge);
