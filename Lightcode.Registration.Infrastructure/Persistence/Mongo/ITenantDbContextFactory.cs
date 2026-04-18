namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public interface ITenantDbContextFactory
{
    TenantMongoDbContext Create();
}
