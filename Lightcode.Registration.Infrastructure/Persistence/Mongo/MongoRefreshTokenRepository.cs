using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Domain.Entities;
using Lightcode.Registration.Infrastructure.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoRefreshTokenRepository(
    IMongoClient client,
    ITenantLookup tenantLookup,
    ISecureTokenGenerator tokenGenerator) : IRefreshTokenRepository
{
    public async Task<(string PlainToken, RefreshToken Entity)> CreateAsync(
        string tenantId,
        string subjectId,
        string subjectType,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> scopes,
        DateTime expiresAtUtc,
        int maxUses,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant '{tenantId}' não encontrado.");

        var plain = tokenGenerator.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid().ToString("N"),
            TokenHash = TokenHashing.HashRefreshToken(plain),
            SubjectId = subjectId,
            SubjectType = subjectType,
            Roles = roles,
            Scopes = scopes,
            ExpiresAtUtc = expiresAtUtc,
            UseCount = 0,
            MaxUses = maxUses,
            CreatedAtUtc = DateTime.UtcNow
        };

        var coll = GetCollection(tenant.DatabaseName);
        await coll.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return (plain, entity);
    }

    public async Task<RefreshToken?> FindActiveByPlainTokenAsync(
        string tenantId,
        string plainToken,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return null;

        var hash = TokenHashing.HashRefreshToken(plainToken);
        var coll = GetCollection(tenant.DatabaseName);
        var now = DateTime.UtcNow;

        var token = await coll
            .Find(x => x.TokenHash == hash && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null || token.UseCount >= token.MaxUses)
            return null;

        return token;
    }

    public async Task<bool> TryIncrementUseCountAsync(
        string tenantId,
        string id,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return false;

        var coll = GetCollection(tenant.DatabaseName);
        var now = DateTime.UtcNow;
        var filter = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("_id", id),
            new BsonDocument("revokedAtUtc", BsonNull.Value),
            new BsonDocument("expiresAtUtc", new BsonDocument("$gt", now)),
            new BsonDocument("$expr", new BsonDocument("$lt", new BsonArray { "$useCount", "$maxUses" }))
        });

        var update = Builders<RefreshToken>.Update.Inc(x => x.UseCount, 1);
        var result = await coll.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private IMongoCollection<RefreshToken> GetCollection(string databaseName) =>
        client.GetDatabase(databaseName).GetCollection<RefreshToken>(RefreshToken.CollectionName);
}
