using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Domain.Entities;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoEmailTemplateRepository(IMongoClient client, ITenantLookup tenantLookup)
    : IEmailTemplateRepository
{
    private const string CollectionName = "EmailTemplates";

    public async Task<IReadOnlyList<EmailTemplate>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return [];

        return await coll.Find(_ => true).SortBy(x => x.Key).ToListAsync(cancellationToken);
    }

    public async Task<EmailTemplate?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return null;

        return await coll.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmailTemplate?> GetByKeyAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return null;

        return await coll.Find(x => x.Key == key).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertAsync(EmailTemplate entity, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionRequiredAsync(entity.TenantId, cancellationToken);
        TryEnsureIndexes(coll);
        await coll.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    public async Task ReplaceAsync(EmailTemplate entity, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionRequiredAsync(entity.TenantId, cancellationToken);
        await coll.ReplaceOneAsync(x => x.Id == entity.Id, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        if (coll is null)
            return;

        await coll.DeleteOneAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<IMongoCollection<EmailTemplate>?> GetCollectionAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return null;

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<EmailTemplate>(CollectionName);
        TryEnsureIndexes(coll);
        return coll;
    }

    private async Task<IMongoCollection<EmailTemplate>> GetCollectionRequiredAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var coll = await GetCollectionAsync(tenantId, cancellationToken);
        return coll ?? throw new InvalidOperationException($"Tenant '{tenantId}' não encontrado.");
    }

    private static void TryEnsureIndexes(IMongoCollection<EmailTemplate> collection)
    {
        try
        {
            var keys = Builders<EmailTemplate>.IndexKeys.Ascending(x => x.Key);
            var model = new CreateIndexModel<EmailTemplate>(keys, new CreateIndexOptions { Unique = true, Name = "ux_key" });
            collection.Indexes.CreateOne(model);
        }
        catch (MongoCommandException)
        {
            // índice já existe ou nome duplicado
        }
    }
}
