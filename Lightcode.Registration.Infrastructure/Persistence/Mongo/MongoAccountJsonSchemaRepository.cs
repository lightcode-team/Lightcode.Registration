using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoAccountJsonSchemaRepository : IAccountJsonSchemaRepository
{
    private readonly IMongoCollection<AccountJsonSchema> _collection;

    public MongoAccountJsonSchemaRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var db = client.GetDatabase(options.Value.MasterDatabaseName);
        _collection = db.GetCollection<AccountJsonSchema>("AccountJsonSchemas");
    }

    public async Task<IReadOnlyList<AccountJsonSchema>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var list = await _collection.Find(x => x.TenantId == tenantId).SortBy(x => x.Key).ToListAsync(cancellationToken);
        return list;
    }

    public async Task<AccountJsonSchema?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var doc = await _collection.Find(x => x.TenantId == tenantId && x.Id == id).FirstOrDefaultAsync(cancellationToken);
        return doc;
    }

    public async Task<AccountJsonSchema?> GetByKeyAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        var doc = await _collection.Find(x => x.TenantId == tenantId && x.Key == key).FirstOrDefaultAsync(cancellationToken);
        return doc;
    }

    public async Task<AccountJsonSchema?> GetDefaultAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var doc = await _collection.Find(x => x.TenantId == tenantId && x.IsDefault).FirstOrDefaultAsync(cancellationToken);
        return doc;
    }

    public Task InsertAsync(AccountJsonSchema entity, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public Task ReplaceAsync(AccountJsonSchema entity, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(x => x.Id == entity.Id && x.TenantId == entity.TenantId, entity, cancellationToken: cancellationToken);

    public Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) =>
        _collection.DeleteOneAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task ClearDefaultFlagForTenantAsync(string tenantId, CancellationToken cancellationToken = default) =>
        _collection.UpdateManyAsync(
            x => x.TenantId == tenantId && x.IsDefault,
            Builders<AccountJsonSchema>.Update.Set(x => x.IsDefault, false).Set(x => x.UpdatedAtUtc, DateTime.UtcNow),
            cancellationToken: cancellationToken);
}
