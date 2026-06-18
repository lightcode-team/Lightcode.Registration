using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Domain.Entities;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoFrontConfigRepository(
    IMongoClient client,
    ITenantLookup tenantLookup) : IFrontConfigRepository
{
    private const string CollectionName = "FrontConfig";

    public async Task<FrontConfig?> GetActiveAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return null;

        var collection = GetCollection(tenant);

        var defaultConfig = await collection
            .Find(x => x.Id == FrontConfig.DefaultId && x.Active)
            .FirstOrDefaultAsync(cancellationToken);

        if (defaultConfig is not null)
            return defaultConfig;

        return await collection
            .Find(x => x.Active)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IMongoCollection<FrontConfig> GetCollection(Tenant tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
            return client.GetDatabase(tenant.DatabaseName).GetCollection<FrontConfig>(CollectionName);

        var dedicated = new MongoClient(tenant.ConnectionString);
        return dedicated.GetDatabase(tenant.DatabaseName).GetCollection<FrontConfig>(CollectionName);
    }
}
