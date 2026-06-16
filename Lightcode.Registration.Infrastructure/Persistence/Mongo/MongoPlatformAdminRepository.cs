using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoPlatformAdminRepository : IPlatformAdminRepository
{
    private readonly IMongoCollection<PlatformAdmin> _admins;
    private readonly IMongoCollection<PlatformAdminTenantLink> _links;
    private readonly IMongoCollection<PlatformAdminInvite> _invites;
    private readonly IMongoCollection<Tenant> _tenants;

    public MongoPlatformAdminRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var master = client.GetDatabase(options.Value.MasterDatabaseName);
        _admins = master.GetCollection<PlatformAdmin>(PlatformAdmin.CollectionName);
        _links = master.GetCollection<PlatformAdminTenantLink>(PlatformAdminTenantLink.CollectionName);
        _invites = master.GetCollection<PlatformAdminInvite>(PlatformAdminInvite.CollectionName);
        _tenants = master.GetCollection<Tenant>("Tenants");
    }

    public async Task<PlatformAdmin?> FindAdminByEmailAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        await _admins.Find(x => x.Email == email).FirstOrDefaultAsync(cancellationToken);

    public async Task<PlatformAdmin?> GetAdminByIdAsync(
        string adminId,
        CancellationToken cancellationToken = default) =>
        await _admins.Find(x => x.Id == adminId).FirstOrDefaultAsync(cancellationToken);

    public Task InsertAdminAsync(PlatformAdmin admin, CancellationToken cancellationToken = default) =>
        _admins.InsertOneAsync(admin, cancellationToken: cancellationToken);

    public Task ReplaceAdminAsync(PlatformAdmin admin, CancellationToken cancellationToken = default) =>
        _admins.ReplaceOneAsync(x => x.Id == admin.Id, admin, cancellationToken: cancellationToken);

    public Task InsertInviteAsync(PlatformAdminInvite invite, CancellationToken cancellationToken = default) =>
        _invites.InsertOneAsync(invite, cancellationToken: cancellationToken);

    public async Task<PlatformAdminInvite?> FindPendingInviteByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        await _invites
            .Find(x => x.TokenHash == tokenHash && x.Status == PlatformAdminInviteStatuses.Pending)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task MarkInviteUsedAsync(string inviteId, CancellationToken cancellationToken = default)
    {
        var update = Builders<PlatformAdminInvite>.Update
            .Set(x => x.Status, PlatformAdminInviteStatuses.Used)
            .Set(x => x.UsedAtUtc, DateTime.UtcNow);

        await _invites.UpdateOneAsync(x => x.Id == inviteId, update, cancellationToken: cancellationToken);
    }

    public async Task UpsertTenantLinkAsync(
        string adminId,
        string tenantId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var update = Builders<PlatformAdminTenantLink>.Update
            .SetOnInsert(x => x.Id, Guid.NewGuid().ToString("N"))
            .SetOnInsert(x => x.CreatedAtUtc, now)
            .Set(x => x.AdminId, adminId)
            .Set(x => x.TenantId, tenantId)
            .Set(x => x.Role, role)
            .Set(x => x.Active, true)
            .Set(x => x.UpdatedAtUtc, now);

        await _links.UpdateOneAsync(
            x => x.AdminId == adminId && x.TenantId == tenantId,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<PlatformAdminTenantLink?> FindActiveTenantLinkAsync(
        string adminId,
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await _links
            .Find(x => x.AdminId == adminId && x.TenantId == tenantId && x.Active)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PlatformTenantDto>> ListActiveTenantsForAdminAsync(
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var links = await _links
            .Find(x => x.AdminId == adminId && x.Active)
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
            return [];

        var tenantIds = links.Select(x => x.TenantId).Distinct(StringComparer.Ordinal).ToList();
        var tenants = await _tenants
            .Find(x => tenantIds.Contains(x.Id) && x.Active)
            .ToListAsync(cancellationToken);

        var roles = links.ToDictionary(x => x.TenantId, x => x.Role, StringComparer.Ordinal);
        return tenants
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PlatformTenantDto(
                x.Id,
                x.Name,
                roles.GetValueOrDefault(x.Id, PlatformAdminTenantRoles.Owner)))
            .ToList();
    }
}
