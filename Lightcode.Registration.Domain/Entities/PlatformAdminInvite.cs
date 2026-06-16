namespace Lightcode.Registration.Domain.Entities;

public sealed class PlatformAdminInvite
{
    public const string CollectionName = "PlatformAdminInvites";

    public string Id { get; set; } = default!;

    public string AdminId { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string TokenHash { get; set; } = default!;

    public string Status { get; set; } = PlatformAdminInviteStatuses.Pending;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }
}

public static class PlatformAdminInviteStatuses
{
    public const string Pending = "pending";
    public const string Used = "used";
    public const string Expired = "expired";
}
