namespace Lightcode.Registration.Domain.Entities;

public sealed class TwoFactorChallenge
{
    public const string CollectionName = "TwoFactorChallenges";

    public string Id { get; set; } = default!;

    public string SubjectType { get; set; } = default!;

    public string SubjectId { get; set; } = default!;

    public string? TenantId { get; set; }

    public string Purpose { get; set; } = default!;

    public string Method { get; set; } = default!;

    public string DestinationHint { get; set; } = default!;

    public string CodeHash { get; set; } = default!;

    public string Status { get; set; } = TwoFactorChallengeStatuses.Pending;

    public int Attempts { get; set; }

    public int MaxAttempts { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }
}

public static class TwoFactorSubjectTypes
{
    public const string TenantUser = "tenant_user";
    public const string PlatformAdmin = "platform_admin";
}

public static class TwoFactorChallengePurposes
{
    public const string Login = "login";
    public const string EnableTwoFactor = "enable_2fa";
    public const string DisableTwoFactor = "disable_2fa";
}

public static class TwoFactorMethods
{
    public const string EmailCode = "email_code";
    public const string Totp = "totp";
}

public static class TwoFactorChallengeStatuses
{
    public const string Pending = "pending";
    public const string Used = "used";
    public const string Expired = "expired";
    public const string Failed = "failed";
}
