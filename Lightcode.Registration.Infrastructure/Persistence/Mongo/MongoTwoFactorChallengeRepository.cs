using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoTwoFactorChallengeRepository(
    IMongoClient client,
    ITenantLookup tenantLookup,
    IOptions<MongoOptions> options) : ITwoFactorChallengeRepository
{
    public async Task InsertAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(challenge.TenantId, cancellationToken);
        await EnsureIndexesAsync(collection, cancellationToken);
        await collection.InsertOneAsync(challenge, cancellationToken: cancellationToken);
    }

    public async Task InvalidatePendingAsync(
        string? tenantId,
        string subjectType,
        string subjectId,
        string purpose,
        string method,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(tenantId, cancellationToken);
        var update = Builders<TwoFactorChallenge>.Update
            .Set(x => x.Status, TwoFactorChallengeStatuses.Expired);

        await collection.UpdateManyAsync(
            x => x.TenantId == tenantId
                 && x.SubjectType == subjectType
                 && x.SubjectId == subjectId
                 && x.Purpose == purpose
                 && x.Method == method
                 && x.Status == TwoFactorChallengeStatuses.Pending,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<TwoFactorChallenge?> FindAsync(
        string challengeId,
        string? tenantId,
        string subjectType,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(tenantId, cancellationToken);
        return await collection
            .Find(x => x.Id == challengeId
                       && x.TenantId == tenantId
                       && x.SubjectType == subjectType
                       && x.Purpose == purpose)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryMarkUsedAsync(
        string challengeId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(tenantId, cancellationToken);

        var now = DateTime.UtcNow;
        var update = Builders<TwoFactorChallenge>.Update
            .Set(x => x.Status, TwoFactorChallengeStatuses.Used)
            .Set(x => x.ConsumedAtUtc, now);

        var result = await collection.UpdateOneAsync(
            x => x.Id == challengeId
                 && x.Status == TwoFactorChallengeStatuses.Pending
                 && x.ExpiresAtUtc > now
                 && x.Attempts < x.MaxAttempts,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    public async Task<bool> TryIncrementAttemptsAsync(
        string challengeId,
        string? tenantId,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetCollectionAsync(tenantId, cancellationToken);

        var update = Builders<TwoFactorChallenge>.Update.Inc(x => x.Attempts, 1);
        var result = await collection.UpdateOneAsync(
            x => x.Id == challengeId
                 && x.Status == TwoFactorChallengeStatuses.Pending
                 && x.Attempts < maxAttempts,
            update,
            cancellationToken: cancellationToken);

        await collection.UpdateOneAsync(
            x => x.Id == challengeId
                 && x.Status == TwoFactorChallengeStatuses.Pending
                 && x.Attempts >= maxAttempts,
            Builders<TwoFactorChallenge>.Update.Set(x => x.Status, TwoFactorChallengeStatuses.Failed),
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    private async Task<IMongoCollection<TwoFactorChallenge>> GetCollectionAsync(
        string? tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return client.GetDatabase(options.Value.MasterDatabaseName)
                .GetCollection<TwoFactorChallenge>(TwoFactorChallenge.CollectionName);

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant não encontrado.");

        return client.GetDatabase(tenant.DatabaseName)
            .GetCollection<TwoFactorChallenge>(TwoFactorChallenge.CollectionName);
    }

    private static async Task EnsureIndexesAsync(
        IMongoCollection<TwoFactorChallenge> collection,
        CancellationToken cancellationToken)
    {
        var keys = Builders<TwoFactorChallenge>.IndexKeys.Ascending(x => x.ExpiresAtUtc);
        var model = new CreateIndexModel<TwoFactorChallenge>(
            keys,
            new CreateIndexOptions
            {
                Name = "ttl_expires_at",
                ExpireAfter = TimeSpan.Zero
            });

        try
        {
            await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException)
        {
            // Index can already exist with compatible options.
        }
    }
}
