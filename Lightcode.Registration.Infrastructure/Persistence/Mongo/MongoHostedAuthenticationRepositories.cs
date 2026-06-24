using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoHostedAuthTransactionRepository(
    IMongoClient client,
    IOptions<MongoOptions> options) : IHostedAuthTransactionRepository
{
    public async Task InsertAsync(HostedAuthTransaction transaction, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        await EnsureIndexesAsync(collection, cancellationToken);
        await collection.InsertOneAsync(transaction, cancellationToken: cancellationToken);
    }

    public async Task<HostedAuthTransaction?> FindActiveAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await GetCollection()
            .Find(x => x.Id == id && x.ExpiresAtUtc > now)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IMongoCollection<HostedAuthTransaction> GetCollection() =>
        client.GetDatabase(options.Value.MasterDatabaseName)
            .GetCollection<HostedAuthTransaction>(HostedAuthTransaction.CollectionName);

    private static Task EnsureIndexesAsync(
        IMongoCollection<HostedAuthTransaction> collection,
        CancellationToken cancellationToken) =>
        collection.Indexes.CreateOneAsync(
            new CreateIndexModel<HostedAuthTransaction>(
                Builders<HostedAuthTransaction>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
                new CreateIndexOptions { Name = "ttl_expires_at", ExpireAfter = TimeSpan.Zero }),
            cancellationToken: cancellationToken);
}

public sealed class MongoHostedAuthSessionRepository(
    IMongoClient client,
    IOptions<MongoOptions> options) : IHostedAuthSessionRepository
{
    public async Task InsertAsync(HostedAuthSession session, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        await EnsureIndexesAsync(collection, cancellationToken);
        await collection.InsertOneAsync(session, cancellationToken: cancellationToken);
    }

    public async Task<HostedAuthSession?> FindActiveByTransactionIdAsync(
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await GetCollection()
            .Find(x => x.TransactionId == transactionId && x.ExpiresAtUtc > now)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task ReplaceAsync(HostedAuthSession session, CancellationToken cancellationToken = default)
    {
        await GetCollection().ReplaceOneAsync(
            x => x.Id == session.Id && x.ExpiresAtUtc > DateTime.UtcNow,
            session,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryBeginCompletionAsync(
        string id,
        string expectedStage,
        CancellationToken cancellationToken = default)
    {
        var result = await GetCollection().UpdateOneAsync(
            x => x.Id == id && x.Stage == expectedStage && x.ExpiresAtUtc > DateTime.UtcNow,
            Builders<HostedAuthSession>.Update.Set(x => x.Stage, HostedAuthStages.Completing),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private IMongoCollection<HostedAuthSession> GetCollection() =>
        client.GetDatabase(options.Value.MasterDatabaseName)
            .GetCollection<HostedAuthSession>(HostedAuthSession.CollectionName);

    private static Task EnsureIndexesAsync(
        IMongoCollection<HostedAuthSession> collection,
        CancellationToken cancellationToken) =>
        collection.Indexes.CreateOneAsync(
            new CreateIndexModel<HostedAuthSession>(
                Builders<HostedAuthSession>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
                new CreateIndexOptions { Name = "ttl_expires_at", ExpireAfter = TimeSpan.Zero }),
            cancellationToken: cancellationToken);
}

public sealed class MongoAuthorizationCodeRepository(
    IMongoClient client,
    IOptions<MongoOptions> options) : IAuthorizationCodeRepository
{
    public async Task InsertAsync(AuthorizationCodeGrant grant, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        await EnsureIndexesAsync(collection, cancellationToken);
        await collection.InsertOneAsync(grant, cancellationToken: cancellationToken);
    }

    public async Task<AuthorizationCodeGrant?> TryConsumeAsync(
        string tenantId,
        string codeHash,
        string clientId,
        string redirectUri,
        string codeChallenge,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await GetCollection().FindOneAndUpdateAsync(
            x => x.TenantId == tenantId
                 && x.CodeHash == codeHash
                 && x.ClientId == clientId
                 && x.RedirectUri == redirectUri
                 && x.CodeChallenge == codeChallenge
                 && x.CodeChallengeMethod == "S256"
                 && x.ExpiresAtUtc > now
                 && x.ConsumedAtUtc == null,
            Builders<AuthorizationCodeGrant>.Update.Set(x => x.ConsumedAtUtc, now),
            new FindOneAndUpdateOptions<AuthorizationCodeGrant>
            {
                ReturnDocument = ReturnDocument.Before
            },
            cancellationToken);
    }

    private IMongoCollection<AuthorizationCodeGrant> GetCollection() =>
        client.GetDatabase(options.Value.MasterDatabaseName)
            .GetCollection<AuthorizationCodeGrant>(AuthorizationCodeGrant.CollectionName);

    private static Task EnsureIndexesAsync(
        IMongoCollection<AuthorizationCodeGrant> collection,
        CancellationToken cancellationToken) =>
        collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AuthorizationCodeGrant>(
                Builders<AuthorizationCodeGrant>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
                new CreateIndexOptions { Name = "ttl_expires_at", ExpireAfter = TimeSpan.Zero }),
            cancellationToken: cancellationToken);
}

public sealed class MongoAuthAuditLogRepository(
    IMongoClient client,
    IOptions<MongoOptions> options) : IAuthAuditLogRepository
{
    public async Task InsertAsync(AuthAuditLog entry, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        await EnsureIndexesAsync(collection, cancellationToken);
        await collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
    }

    private IMongoCollection<AuthAuditLog> GetCollection() =>
        client.GetDatabase(options.Value.MasterDatabaseName)
            .GetCollection<AuthAuditLog>(AuthAuditLog.CollectionName);

    private static async Task EnsureIndexesAsync(
        IMongoCollection<AuthAuditLog> collection,
        CancellationToken cancellationToken)
    {
        var models = new[]
        {
            new CreateIndexModel<AuthAuditLog>(
                Builders<AuthAuditLog>.IndexKeys.Ascending(x => x.CorrelationId),
                new CreateIndexOptions { Name = "correlation_id" }),
            new CreateIndexModel<AuthAuditLog>(
                Builders<AuthAuditLog>.IndexKeys.Ascending(x => x.CreatedAtUtc),
                new CreateIndexOptions { Name = "created_at_utc" })
        };

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}

public sealed class MongoSsoSessionRepository(
    IMongoClient client,
    IOptions<MongoOptions> options) : ISsoSessionRepository
{
    public async Task InsertAsync(SsoSession session, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        await EnsureIndexesAsync(collection, cancellationToken);
        await collection.InsertOneAsync(session, cancellationToken: cancellationToken);
    }

    public async Task<SsoSession?> FindActiveAsync(
        string id,
        string tenantId,
        DateTime idleCutoffUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        await GetCollection()
            .Find(x => x.Id == id
                       && x.TenantId == tenantId
                       && x.RevokedAtUtc == null
                       && x.ExpiresAtUtc > nowUtc
                       && x.LastSeenAtUtc > idleCutoffUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task TouchAsync(string id, DateTime lastSeenAtUtc, CancellationToken cancellationToken = default) =>
        GetCollection().UpdateOneAsync(
            x => x.Id == id && x.RevokedAtUtc == null && x.ExpiresAtUtc > lastSeenAtUtc,
            Builders<SsoSession>.Update.Set(x => x.LastSeenAtUtc, lastSeenAtUtc),
            cancellationToken: cancellationToken);

    public Task RevokeAsync(string id, DateTime revokedAtUtc, CancellationToken cancellationToken = default) =>
        GetCollection().UpdateOneAsync(
            x => x.Id == id && x.RevokedAtUtc == null,
            Builders<SsoSession>.Update.Set(x => x.RevokedAtUtc, revokedAtUtc),
            cancellationToken: cancellationToken);

    public Task RevokeBySubjectAsync(
        string tenantId,
        string subjectId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default) =>
        GetCollection().UpdateManyAsync(
            x => x.TenantId == tenantId && x.SubjectId == subjectId && x.RevokedAtUtc == null,
            Builders<SsoSession>.Update.Set(x => x.RevokedAtUtc, revokedAtUtc),
            cancellationToken: cancellationToken);

    private IMongoCollection<SsoSession> GetCollection() =>
        client.GetDatabase(options.Value.MasterDatabaseName)
            .GetCollection<SsoSession>(SsoSession.CollectionName);

    private static async Task EnsureIndexesAsync(
        IMongoCollection<SsoSession> collection,
        CancellationToken cancellationToken)
    {
        var models = new[]
        {
            new CreateIndexModel<SsoSession>(
                Builders<SsoSession>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
                new CreateIndexOptions { Name = "ttl_expires_at", ExpireAfter = TimeSpan.Zero }),
            new CreateIndexModel<SsoSession>(
                Builders<SsoSession>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectId),
                new CreateIndexOptions { Name = "tenant_subject" })
        };

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
