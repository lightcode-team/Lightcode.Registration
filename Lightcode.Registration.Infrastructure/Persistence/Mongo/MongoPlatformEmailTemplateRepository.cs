using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoPlatformEmailTemplateRepository(
    IMongoClient client,
    IOptions<MongoOptions> options) : IPlatformEmailTemplateRepository
{
    private const string CollectionName = "EmailTemplates";

    public async Task<EmailTemplate?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var coll = GetCollection();
        TryEnsureIndexes(coll);
        return await coll.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmailTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var coll = GetCollection();
        TryEnsureIndexes(coll);
        return await coll.Find(x => x.Key == key).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InsertIfMissingAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        var coll = GetCollection();
        TryEnsureIndexes(coll);

        var existing = await coll.Find(x => x.Key == template.Key).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return;

        await coll.InsertOneAsync(template, cancellationToken: cancellationToken);
    }

    private IMongoCollection<EmailTemplate> GetCollection() =>
        client.GetDatabase(options.Value.MasterDatabaseName).GetCollection<EmailTemplate>(CollectionName);

    private static void TryEnsureIndexes(IMongoCollection<EmailTemplate> collection)
    {
        try
        {
            var keys = Builders<EmailTemplate>.IndexKeys.Ascending(x => x.Key);
            var model = new CreateIndexModel<EmailTemplate>(
                keys,
                new CreateIndexOptions { Unique = true, Name = "ux_key" });
            collection.Indexes.CreateOne(model);
        }
        catch (MongoCommandException)
        {
            // Indice ja existe ou nome duplicado.
        }
    }
}
