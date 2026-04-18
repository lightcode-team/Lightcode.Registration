using Lightcode.Registration.Domain.Entities;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class TenantMongoDbContext
{
    private readonly IMongoDatabase _database;

    public TenantMongoDbContext(IMongoClient sharedClient, Tenant tenant, string fallbackConnectionString)
    {
        var connectionString = string.IsNullOrWhiteSpace(tenant.ConnectionString)
            ? fallbackConnectionString
            : tenant.ConnectionString;

        if (string.IsNullOrWhiteSpace(tenant.ConnectionString))
        {
            _database = sharedClient.GetDatabase(tenant.DatabaseName);
            return;
        }

        var dedicated = new MongoClient(connectionString);
        _database = dedicated.GetDatabase(tenant.DatabaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string? collectionName = null) =>
        _database.GetCollection<T>(collectionName ?? typeof(T).Name);
}
