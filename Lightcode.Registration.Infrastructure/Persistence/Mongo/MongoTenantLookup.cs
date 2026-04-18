using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoTenantLookup(IMongoClient client, IOptions<MongoOptions> options) : ITenantLookup
{
    private readonly IMongoCollection<Tenant> _tenants = client
        .GetDatabase(options.Value.MasterDatabaseName)
        .GetCollection<Tenant>("Tenants");

    public async Task<Tenant?> FindActiveByIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenants.Find(x => x.Id == tenantId && x.Active).FirstOrDefaultAsync(cancellationToken);
        return tenant;
    }

    public async Task<IReadOnlyList<Tenant>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var list = await _tenants.Find(x => x.Active).ToListAsync(cancellationToken);
        return list;
    }
}
