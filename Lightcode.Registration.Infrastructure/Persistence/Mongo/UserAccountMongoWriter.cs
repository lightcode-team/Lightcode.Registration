using Lightcode.Registration.Application.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class UserAccountMongoWriter(IMongoClient client, ITenantLookup tenantLookup) : IUserAccountWriter
{
    public async Task<bool> EmailExistsAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return false;

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var count = await coll.CountDocumentsAsync(
            new BsonDocument("email", email),
            new CountOptions { Limit = 1 },
            cancellationToken);
        return count > 0;
    }

    public async Task InsertAsync(string tenantId, string documentJson, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant não encontrado.");

        var doc = BsonDocument.Parse(documentJson);
        if (!doc.Contains("_id"))
            doc["_id"] = ObjectId.GenerateNewId();

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        await coll.InsertOneAsync(doc, cancellationToken: cancellationToken);
    }
}
