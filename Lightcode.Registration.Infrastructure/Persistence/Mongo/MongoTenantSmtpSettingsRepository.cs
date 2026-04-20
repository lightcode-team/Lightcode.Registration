using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Domain.Entities;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoTenantSmtpSettingsRepository(IMongoClient client, ITenantLookup tenantLookup)
    : ITenantSmtpSettingsRepository
{
    public async Task<TenantSmtpConfiguration?> GetSmtpAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return null;

        var coll = client
            .GetDatabase(tenant.DatabaseName)
            .GetCollection<TenantSmtpSettingsRoot>(TenantSmtpSettingsRoot.CollectionName);

        var root = await coll
            .Find(x => x.Id == TenantSmtpSettingsRoot.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        return root?.Smtp;
    }
}
