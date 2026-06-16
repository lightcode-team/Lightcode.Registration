using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IPlatformAdminRepository
{
    Task<PlatformAdmin?> FindAdminByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<PlatformAdmin?> GetAdminByIdAsync(string adminId, CancellationToken cancellationToken = default);

    Task InsertAdminAsync(PlatformAdmin admin, CancellationToken cancellationToken = default);

    Task ReplaceAdminAsync(PlatformAdmin admin, CancellationToken cancellationToken = default);

    Task InsertInviteAsync(PlatformAdminInvite invite, CancellationToken cancellationToken = default);

    Task<PlatformAdminInvite?> FindPendingInviteByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task MarkInviteUsedAsync(string inviteId, CancellationToken cancellationToken = default);

    Task UpsertTenantLinkAsync(string adminId, string tenantId, string role, CancellationToken cancellationToken = default);

    Task<PlatformAdminTenantLink?> FindActiveTenantLinkAsync(string adminId, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformTenantDto>> ListActiveTenantsForAdminAsync(string adminId, CancellationToken cancellationToken = default);
}
