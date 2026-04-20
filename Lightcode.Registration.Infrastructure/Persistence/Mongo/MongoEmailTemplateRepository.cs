using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoEmailTemplateRepository : IEmailTemplateRepository
{
    private readonly IMongoCollection<EmailTemplate> _collection;

    public MongoEmailTemplateRepository(IMongoClient client, IOptions<MongoOptions> options)
    {
        var db = client.GetDatabase(options.Value.MasterDatabaseName);
        _collection = db.GetCollection<EmailTemplate>("EmailTemplates");
        TryEnsureIndexes();
    }

    private void TryEnsureIndexes()
    {
        try
        {
            var keys = Builders<EmailTemplate>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Key);
            var model = new CreateIndexModel<EmailTemplate>(keys, new CreateIndexOptions { Unique = true, Name = "ux_tenant_key" });
            _collection.Indexes.CreateOne(model);
        }
        catch (MongoCommandException)
        {
            // índice já existe ou nome duplicado
        }
    }

    public async Task<IReadOnlyList<EmailTemplate>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var list = await _collection.Find(x => x.TenantId == tenantId).SortBy(x => x.Key).ToListAsync(cancellationToken);
        return list;
    }

    public async Task<EmailTemplate?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(x => x.TenantId == tenantId && x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmailTemplate?> GetByKeyAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(x => x.TenantId == tenantId && x.Key == key).FirstOrDefaultAsync(cancellationToken);
    }

    public Task InsertAsync(EmailTemplate entity, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public Task ReplaceAsync(EmailTemplate entity, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(x => x.Id == entity.Id && x.TenantId == entity.TenantId, entity, cancellationToken: cancellationToken);

    public Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) =>
        _collection.DeleteOneAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
}
