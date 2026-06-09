using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Domain.Entities;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoOAuthClientRepository(IMongoClient client, ITenantLookup tenantLookup)
    : IOAuthClientRepository
{
    public async Task<IReadOnlyList<OAuthClient>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return [];

        return await coll.Find(x => x.Active).SortBy(x => x.ClientId).ToListAsync(cancellationToken);
    }

    public async Task<OAuthClient?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return null;

        return await coll.Find(x => x.Id == id && x.Active).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OAuthClient?> FindByClientIdAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return null;

        return await coll
            .Find(x => x.ClientId == clientId && x.Active)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionRequiredAsync(tenantId, cancellationToken);
        await coll.InsertOneAsync(client, cancellationToken: cancellationToken);
    }

    public async Task ReplaceAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionRequiredAsync(tenantId, cancellationToken);
        await coll.ReplaceOneAsync(
            x => x.Id == client.Id && x.Active,
            client,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> DeactivateAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return false;

        var update = Builders<OAuthClient>.Update
            .Set(x => x.Active, false)
            .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);

        var result = await coll.UpdateOneAsync(
            x => x.Id == id && x.Active,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    private async Task<IMongoCollection<OAuthClient>?> GetCollectionAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        return tenant is null
            ? null
            : client.GetDatabase(tenant.DatabaseName).GetCollection<OAuthClient>(OAuthClient.CollectionName);
    }

    private async Task<IMongoCollection<OAuthClient>> GetCollectionRequiredAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        return coll ?? throw new InvalidOperationException($"Tenant '{tenantId}' não encontrado.");
    }
}
