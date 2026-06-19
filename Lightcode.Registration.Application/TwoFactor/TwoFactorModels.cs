namespace Lightcode.Registration.Application.TwoFactor;

public sealed record TwoFactorChallengeSubject(
    string SubjectType,
    string SubjectId,
    string? TenantId,
    string Email,
    string Username);

public sealed record UserTwoFactorSettings(
    bool Enabled,
    string PreferredMethod,
    bool EmailEnabled);
