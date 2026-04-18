using Lightcode.Registration.Application.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoUsersCollectionSchemaApplier(IMongoClient client, ITenantLookup tenantLookup)
    : IUsersCollectionSchemaApplier
{
    public async Task ApplyAsync(string tenantId, string mongoJsonSchemaInnerJson, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant não encontrado.");

        var db = client.GetDatabase(tenant.DatabaseName);
        var inner = BsonDocument.Parse(mongoJsonSchemaInnerJson);
        var validator = new BsonDocument("$jsonSchema", inner);

        using var cursor = await db.ListCollectionNamesAsync(new ListCollectionNamesOptions(), cancellationToken);
        var names = await cursor.ToListAsync(cancellationToken);

        if (!names.Contains("Users"))
        {
            var create = new BsonDocument
            {
                { "create", "Users" },
                { "validator", validator },
                { "validationLevel", "strict" },
                { "validationAction", "error" }
            };
            await db.RunCommandAsync<BsonDocument>(create, cancellationToken: cancellationToken);
            return;
        }

        var collMod = new BsonDocument
        {
            { "collMod", "Users" },
            { "validator", validator },
            { "validationLevel", "strict" },
            { "validationAction", "error" }
        };
        await db.RunCommandAsync<BsonDocument>(collMod, cancellationToken: cancellationToken);
    }
}
