using System.Text.Json;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Registration;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoTenantProvisioner : ITenantProvisioner
{
    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<Tenant> _tenants;
    private readonly IAccountJsonSchemaRepository _accountSchemas;
    private readonly IJsonSchemaToMongoValidatorMapper _mongoMapper;
    private readonly IUsersCollectionSchemaApplier _usersSchemaApplier;
    private readonly IUserAccountWriter _userAccountWriter;
    private readonly IPasswordHasher _passwordHasher;
    private readonly MasterOptions _masterOptions;
    private readonly TenantDefaultSmtpOptions _defaultTenantSmtp;

    public MongoTenantProvisioner(
        IMongoClient client,
        IOptions<MongoOptions> mongoOptions,
        IOptions<MasterOptions> masterOptions,
        IOptions<TenantDefaultSmtpOptions> defaultTenantSmtp,
        IAccountJsonSchemaRepository accountSchemas,
        IJsonSchemaToMongoValidatorMapper mongoMapper,
        IUsersCollectionSchemaApplier usersSchemaApplier,
        IUserAccountWriter userAccountWriter,
        IPasswordHasher passwordHasher)
    {
        _mongoClient = client;
        _accountSchemas = accountSchemas;
        _mongoMapper = mongoMapper;
        _usersSchemaApplier = usersSchemaApplier;
        _userAccountWriter = userAccountWriter;
        _passwordHasher = passwordHasher;
        _masterOptions = masterOptions.Value;
        _defaultTenantSmtp = defaultTenantSmtp.Value;
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

        await SeedTenantSmtpSettingsAsync(dbName, cancellationToken);

        var now = DateTime.UtcNow;
        var defaultSchema = new AccountJsonSchema
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = "default",
            DisplayName = "Registo default",
            ConfigJson = null,
            SchemaJson = DefaultAccountRegistrationSchema.Json.Trim(),
            IsDefault = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _accountSchemas.InsertAsync(defaultSchema, cancellationToken);

        var mongoInner = _mongoMapper.TryMap(defaultSchema.SchemaJson, out _);
        if (!string.IsNullOrEmpty(mongoInner))
            await _usersSchemaApplier.ApplyAsync(tenant.Id, mongoInner, cancellationToken);

        await TrySeedBootstrapAdminAsync(tenant.Id, cancellationToken);

        return tenant;
    }

    private async Task SeedTenantSmtpSettingsAsync(string tenantDatabaseName, CancellationToken cancellationToken)
    {
        var from = string.IsNullOrWhiteSpace(_defaultTenantSmtp.EmailRemetente)
            ? _defaultTenantSmtp.Usuario
            : _defaultTenantSmtp.EmailRemetente;

        var doc = new TenantSmtpSettingsRoot
        {
            Id = TenantSmtpSettingsRoot.DocumentId,
            Smtp = new TenantSmtpConfiguration
            {
                Host = _defaultTenantSmtp.Host,
                Port = _defaultTenantSmtp.Port,
                Usuario = _defaultTenantSmtp.Usuario,
                Senha = _defaultTenantSmtp.Senha,
                EmailRemetente = from,
                NomeRemetente = _defaultTenantSmtp.NomeRemetente,
                UsarSsl = _defaultTenantSmtp.UsarSsl
            }
        };

        var coll = _mongoClient
            .GetDatabase(tenantDatabaseName)
            .GetCollection<TenantSmtpSettingsRoot>(TenantSmtpSettingsRoot.CollectionName);

        await coll.InsertOneAsync(doc, cancellationToken: cancellationToken);
    }

    private async Task TrySeedBootstrapAdminAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_masterOptions.TenantBootstrapAdminPassword))
            return;

        var username = _masterOptions.TenantBootstrapAdminUsername.Trim().ToLowerInvariant();
        if (await _userAccountWriter.UsernameExistsAsync(tenantId, username, cancellationToken))
            return;

        var email = "admin@localhost";
        if (await _userAccountWriter.EmailExistsAsync(tenantId, email, cancellationToken))
            return;

        var hash = _passwordHasher.Hash(_masterOptions.TenantBootstrapAdminPassword);
        var payload = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["username"] = username,
            ["password"] = hash,
            ["roles"] = new[] { UserRoles.Admin },
            ["createdAtUtc"] = DateTime.UtcNow,
            ["status"] = AccountStatuses.Active
        };

        var json = JsonSerializer.Serialize(payload);
        await _userAccountWriter.InsertAsync(tenantId, json, cancellationToken);
    }
}
