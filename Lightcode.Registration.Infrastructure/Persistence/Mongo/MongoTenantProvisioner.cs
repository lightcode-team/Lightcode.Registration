using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Registration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoTenantProvisioner : ITenantProvisioner
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<Tenant> _tenants;
    private readonly IAccountJsonSchemaRepository _accountSchemas;
    private readonly IJsonSchemaToMongoValidatorMapper _mongoMapper;
    private readonly IUsersCollectionSchemaApplier _usersSchemaApplier;

    public MongoTenantProvisioner(
        IMongoClient client,
        IOptions<MongoOptions> mongoOptions,
        IAccountJsonSchemaRepository accountSchemas,
        IJsonSchemaToMongoValidatorMapper mongoMapper,
        IUsersCollectionSchemaApplier usersSchemaApplier)
    {
        _client = client;
        _accountSchemas = accountSchemas;
        _mongoMapper = mongoMapper;
        _usersSchemaApplier = usersSchemaApplier;
        var mongo = mongoOptions.Value;
        var master = client.GetDatabase(mongo.MasterDatabaseName);
        _tenants = master.GetCollection<Tenant>("Tenants");
    }

    public async Task<Tenant> ProvisionAsync(string name, CancellationToken cancellationToken = default)
    {
        var tenantId = Guid.NewGuid().ToString("N");
        var dbName = $"tenant_{tenantId}";

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = name,
            DatabaseName = dbName,
            ConnectionString = null,
            Active = true
        };

        await _tenants.InsertOneAsync(tenant, cancellationToken: cancellationToken);

        var now = DateTime.UtcNow;
        var defaultSchema = new AccountJsonSchema
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = "default",
            DisplayName = "Registo default",
            SchemaJson = DefaultAccountRegistrationSchema.Json.Trim(),
            IsDefault = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _accountSchemas.InsertAsync(defaultSchema, cancellationToken);

        var mongoInner = _mongoMapper.TryMap(defaultSchema.SchemaJson, out _);
        if (!string.IsNullOrEmpty(mongoInner))
            await _usersSchemaApplier.ApplyAsync(tenant.Id, mongoInner, cancellationToken);

        return tenant;
    }
}
