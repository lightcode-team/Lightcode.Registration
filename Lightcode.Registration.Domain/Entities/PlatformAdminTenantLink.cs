namespace Lightcode.Registration.Domain.Entities;

public sealed class PlatformAdminTenantLink
{
    public const string CollectionName = "PlatformAdminTenantLinks";

    public string Id { get; set; } = default!;

    public string AdminId { get; set; } = default!;

    public string TenantId { get; set; } = default!;

    public string Role { get; set; } = PlatformAdminTenantRoles.Owner;

    public bool Active { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public static class PlatformAdminTenantRoles
{
    public const string Owner = "owner";
}
