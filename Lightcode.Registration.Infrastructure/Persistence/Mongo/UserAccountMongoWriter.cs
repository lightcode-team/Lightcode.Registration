using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class UserAccountMongoWriter(IMongoClient client, ITenantLookup tenantLookup) : IUserAccountWriter
{
    private static readonly JsonWriterSettings RelaxedJsonWriterSettings = new()
    {
        OutputMode = JsonOutputMode.RelaxedExtendedJson
    };

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

    public async Task<bool> UsernameExistsAsync(string tenantId, string username, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return false;

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var count = await coll.CountDocumentsAsync(
            new BsonDocument("username", username),
            new CountOptions { Limit = 1 },
            cancellationToken);
        return count > 0;
    }

    public async Task<string> InsertAsync(string tenantId, string documentJson, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant não encontrado.");

        var doc = BsonDocument.Parse(documentJson);
        if (!doc.Contains("_id"))
            doc["_id"] = ObjectId.GenerateNewId();

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        await coll.InsertOneAsync(doc, cancellationToken: cancellationToken);

        return doc["_id"].ToString()!;
    }

    public async Task<string?> GetUserDocumentJsonAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(userId, out var oid))
            return null;

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return null;

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var doc = await coll.Find(Builders<BsonDocument>.Filter.Eq("_id", oid)).FirstOrDefaultAsync(cancellationToken);
        return doc is null ? null : doc.ToJson(RelaxedJsonWriterSettings);
    }

    public async Task ReplaceUserDocumentAsync(
        string tenantId,
        string userId,
        string documentJson,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(userId, out var oid))
            throw new ArgumentException("userId inválido.", nameof(userId));

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant não encontrado.");

        var newDoc = BsonDocument.Parse(documentJson);
        if (!newDoc.Contains("_id"))
            newDoc["_id"] = oid;
        else if (newDoc["_id"].ToString() != oid.ToString())
            throw new InvalidOperationException("O _id do documento não coincide com o utilizador.");

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var result = await coll.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", oid),
            newDoc,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
            throw new InvalidOperationException("Utilizador não encontrado.");
    }

    public async Task<bool> EmailTakenByOtherUserAsync(
        string tenantId,
        string email,
        string excludeUserId,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(excludeUserId, out var excludeOid))
            return false;

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return false;

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("email", email),
            Builders<BsonDocument>.Filter.Ne("_id", excludeOid));

        var count = await coll.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken);
        return count > 0;
    }

    public async Task<bool> UsernameTakenByOtherUserAsync(
        string tenantId,
        string username,
        string excludeUserId,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(excludeUserId, out var excludeOid))
            return false;

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return false;

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("username", username),
            Builders<BsonDocument>.Filter.Ne("_id", excludeOid));

        var count = await coll.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken);
        return count > 0;
    }

    public async Task MarkExpiryReminderSentAsync(
        string tenantId,
        string userId,
        int reminderKind,
        DateTime sentUtc,
        CancellationToken cancellationToken = default)
    {
        if (reminderKind is not (30 or 15))
            throw new ArgumentOutOfRangeException(nameof(reminderKind));

        if (!ObjectId.TryParse(userId, out var oid))
            return;

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return;

        var field = reminderKind == 30 ? "expiryReminder30SentUtc" : "expiryReminder15SentUtc";
        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var update = Builders<BsonDocument>.Update.Set(field, sentUtc);
        await coll.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", oid),
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryMarkRegistrationExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(userId, out var oid))
            return false;

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return false;

        var now = DateTime.UtcNow;
        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", oid),
            Builders<BsonDocument>.Filter.Ne("status", AccountStatuses.Expired),
            Builders<BsonDocument>.Filter.Exists("registrationExpiresAtUtc"),
            Builders<BsonDocument>.Filter.Lt("registrationExpiresAtUtc", now));

        var update = Builders<BsonDocument>.Update.Set("status", AccountStatuses.Expired);
        var result = await coll.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }
}
