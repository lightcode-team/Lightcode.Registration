using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class TenantDbContextFactory(
    IHttpContextAccessor httpContextAccessor,
    IMongoClient mongoClient,
    IOptions<MongoOptions> mongoOptions) : ITenantDbContextFactory
{
    public TenantMongoDbContext Create()
    {
        var tenant = httpContextAccessor.HttpContext?.Items["Tenant"] as Tenant
            ?? throw new InvalidOperationException("Tenant não resolvido para esta requisição.");

        return new TenantMongoDbContext(mongoClient, tenant, mongoOptions.Value.ConnectionString);
    }
}
