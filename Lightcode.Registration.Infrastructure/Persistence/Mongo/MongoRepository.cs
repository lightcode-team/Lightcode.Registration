using System.Linq.Expressions;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoRepository<T> where T : class
{
    private readonly IMongoCollection<T> _collection;

    public MongoRepository(ITenantDbContextFactory factory, string? collectionName = null)
    {
        var db = factory.Create();
        _collection = db.GetCollection<T>(collectionName);
    }

    public Task InsertAsync(T entity, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(FilterDefinition<T>.Empty).ToListAsync(cancellationToken);

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) =>
        await _collection.Find(filter).ToListAsync(cancellationToken);
}
