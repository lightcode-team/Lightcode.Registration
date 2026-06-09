using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Registration;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoTenantProvisioner : ITenantProvisioner
{
    private const string ClientCredentialsTemplateKey = "client-credentials-secret";

    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<Tenant> _tenants;
    private readonly IMongoCollection<EmailTemplate> _emailTemplates;
    private readonly IAccountJsonSchemaRepository _accountSchemas;
    private readonly IJsonSchemaToMongoValidatorMapper _mongoMapper;
    private readonly IUsersCollectionSchemaApplier _usersSchemaApplier;
    private readonly IOAuthClientRepository _oauthClientRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly JwtOptions _jwtOptions;
    private readonly TenantDefaultSmtpOptions _defaultTenantSmtp;

    public MongoTenantProvisioner(
        IMongoClient client,
        IOptions<MongoOptions> mongoOptions,
        IOptions<JwtOptions> jwtOptions,
        IOptions<TenantDefaultSmtpOptions> defaultTenantSmtp,
        IAccountJsonSchemaRepository accountSchemas,
        IJsonSchemaToMongoValidatorMapper mongoMapper,
        IUsersCollectionSchemaApplier usersSchemaApplier,
        IOAuthClientRepository oauthClientRepository,
        IPasswordHasher passwordHasher,
        ISecureTokenGenerator tokenGenerator)
    {
        _mongoClient = client;
        _accountSchemas = accountSchemas;
        _mongoMapper = mongoMapper;
        _usersSchemaApplier = usersSchemaApplier;
        _oauthClientRepository = oauthClientRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions.Value;
        _defaultTenantSmtp = defaultTenantSmtp.Value;
        var mongo = mongoOptions.Value;
        var master = client.GetDatabase(mongo.MasterDatabaseName);
        _tenants = master.GetCollection<Tenant>("Tenants");
        _emailTemplates = master.GetCollection<EmailTemplate>("EmailTemplates");
    }

    public async Task<TenantProvisionResult> ProvisionAsync(
        TenantProvisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = Guid.NewGuid().ToString("N");
        var dbName = $"tenant_{tenantId}";

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name,
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

        await SeedClientCredentialsEmailTemplateAsync(tenant, now, cancellationToken);

        var clientId = $"client_{tenantId}";
        var clientSecret = _tokenGenerator.GenerateClientSecret();
        var oauthClient = new OAuthClient
        {
            Id = Guid.NewGuid().ToString("N"),
            ClientId = clientId,
            ClientSecretHash = _passwordHasher.Hash(clientSecret),
            DisplayName = "Cliente principal",
            TokenConfig = CreateDefaultTokenConfig(tenantId),
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _oauthClientRepository.InsertAsync(tenant.Id, oauthClient, cancellationToken);

        return new TenantProvisionResult(tenant, clientId, clientSecret);
    }

    private OAuthClientTokenConfiguration CreateDefaultTokenConfig(string tenantId) =>
        new()
        {
            AccessTokenExpirationMinutes = _jwtOptions.ExpirationMinutes,
            RefreshTokenExpirationDays = _jwtOptions.RefreshTokenExpirationDays,
            MaxRefreshTokenUses = _jwtOptions.MaxRefreshTokenUses,
            Values =
            [
                new OAuthClientTokenClaimValue
                {
                    Type = TokenClaimTypes.Issuer,
                    Value = $"{_jwtOptions.Issuer}/{tenantId}"
                },
                new OAuthClientTokenClaimValue
                {
                    Type = TokenClaimTypes.Audience,
                    Value = $"{_jwtOptions.Audience}/{tenantId}"
                },
                new OAuthClientTokenClaimValue
                {
                    Type = TokenClaimTypes.Scope,
                    Value = OAuthClientsScopes.Owner
                }
            ]
        };

    private async Task SeedClientCredentialsEmailTemplateAsync(
        Tenant tenant,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = ClientCredentialsTemplateKey,
            DisplayName = "Credenciais client_credentials",
            Subject = "Credenciais OAuth — {{tenantName}}",
            HtmlBody = """
                <p>Olá,</p>
                <p>O tenant <strong>{{tenantName}}</strong> foi criado com sucesso.</p>
                <p>Utilize as credenciais abaixo para autenticação <code>client_credentials</code>:</p>
                <ul>
                  <li><strong>Tenant ID:</strong> {{tenantId}}</li>
                  <li><strong>Client ID:</strong> {{clientId}}</li>
                  <li><strong>Client Secret:</strong> {{clientSecret}}</li>
                </ul>
                <p>Guarde o segredo em local seguro; não será reenviado.</p>
                """,
            TextBody = """
                O tenant {{tenantName}} foi criado.

                Tenant ID: {{tenantId}}
                Client ID: {{clientId}}
                Client Secret: {{clientSecret}}

                Guarde o segredo em local seguro; não será reenviado.
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _emailTemplates.InsertOneAsync(template, cancellationToken: cancellationToken);
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
}
