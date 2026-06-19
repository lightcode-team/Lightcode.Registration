namespace Lightcode.Registration.Domain.Entities;

public sealed class PlatformAdmin
{
    public const string CollectionName = "PlatformAdmins";

    public string Id { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string? PasswordHash { get; set; }

    public string Status { get; set; } = PlatformAdminStatuses.PendingActivation;

    public PlatformAdminTwoFactorSettings TwoFactorSettings { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class PlatformAdminTwoFactorSettings
{
    public bool Enabled { get; set; }

    public bool EmailEnabled { get; set; }

    public string PreferredMethod { get; set; } = TwoFactorMethods.EmailCode;

    public DateTime? UpdatedAtUtc { get; set; }
}

public static class PlatformAdminStatuses
{
    public const string PendingActivation = "pending_activation";
    public const string Active = "active";
    public const string Inactive = "inactive";
}
