using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Registration;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Domain.Entities;
using Lightcode.Registration.Infrastructure.Security;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoTenantProvisioner : ITenantProvisioner
{
    private const string ClientCredentialsTemplateKey = "client-credentials-secret";
    private const string PlatformAdminInviteTemplateKey = "platform-admin-invite";
    private const string TenantOnboardingTemplateKey = "tenant-onboarding";

    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<Tenant> _tenants;
    private readonly IEmailTemplateRepository _emailTemplates;
    private readonly IAccountJsonSchemaRepository _accountSchemas;
    private readonly IJsonSchemaToMongoValidatorMapper _mongoMapper;
    private readonly IUsersCollectionSchemaApplier _usersSchemaApplier;
    private readonly IOAuthClientRepository _oauthClientRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly ITenantSigningKeyProtector _signingKeyProtector;
    private readonly JwtOptions _jwtOptions;
    private readonly RegistrationOptions _registrationOptions;
    private readonly TenantDefaultSmtpOptions _defaultTenantSmtp;

    public MongoTenantProvisioner(
        IMongoClient client,
        IOptions<MongoOptions> mongoOptions,
        IOptions<JwtOptions> jwtOptions,
        IOptions<RegistrationOptions> registrationOptions,
        IOptions<TenantDefaultSmtpOptions> defaultTenantSmtp,
        IEmailTemplateRepository emailTemplates,
        IAccountJsonSchemaRepository accountSchemas,
        IJsonSchemaToMongoValidatorMapper mongoMapper,
        IUsersCollectionSchemaApplier usersSchemaApplier,
        IOAuthClientRepository oauthClientRepository,
        IPasswordHasher passwordHasher,
        ISecureTokenGenerator tokenGenerator,
        ITenantSigningKeyProtector signingKeyProtector)
    {
        _mongoClient = client;
        _emailTemplates = emailTemplates;
        _accountSchemas = accountSchemas;
        _mongoMapper = mongoMapper;
        _usersSchemaApplier = usersSchemaApplier;
        _oauthClientRepository = oauthClientRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _signingKeyProtector = signingKeyProtector;
        _jwtOptions = jwtOptions.Value;
        _registrationOptions = registrationOptions.Value;
        _defaultTenantSmtp = defaultTenantSmtp.Value;
        var mongo = mongoOptions.Value;
        var master = client.GetDatabase(mongo.MasterDatabaseName);
        _tenants = master.GetCollection<Tenant>("Tenants");
    }

    public async Task<TenantProvisionResult> ProvisionAsync(
        TenantProvisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = Guid.NewGuid().ToString("N");
        var dbName = $"tenant_{tenantId}";
        var now = DateTime.UtcNow;
        var signingKey = TenantRsaSigningKeyFactory.Create();

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name,
            DatabaseName = dbName,
            ConnectionString = null,
            SigningPrivateKeyEncrypted = _signingKeyProtector.Protect(signingKey.PrivateKeyBase64),
            SigningPublicKeyJwk = signingKey.PublicKeyJwk,
            SigningKeyId = signingKey.KeyId,
            SigningKeyVersion = 1,
            CreatedAt = now,
            SigningKeyCreatedAt = now,
            Active = true
        };

        await _tenants.InsertOneAsync(tenant, cancellationToken: cancellationToken);

        await SeedTenantSmtpSettingsAsync(dbName, cancellationToken);

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
        await SeedTenantOnboardingEmailTemplateAsync(tenant, now, cancellationToken);
        await SeedPlatformAdminInviteEmailTemplateAsync(tenant, now, cancellationToken);
        await SeedEmailConfirmationTemplatesAsync(tenant, now, cancellationToken);
        await SeedPasswordResetEmailTemplateAsync(tenant, now, cancellationToken);

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
                    Value = TenantTokenIssuer.Build(_registrationOptions, _jwtOptions, tenantId)
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

        await _emailTemplates.InsertAsync(template, cancellationToken);
    }

    private async Task SeedPlatformAdminInviteEmailTemplateAsync(
        Tenant tenant,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = PlatformAdminInviteTemplateKey,
            DisplayName = "Convite ADM central",
            Subject = "Convite de administração — {{tenantName}}",
            HtmlBody = """
                <p>Olá,</p>
                <p>Você recebeu acesso administrativo ao painel central.</p>
                <p>Ative o seu acesso pelo link abaixo:</p>
                <p><a href="{{activationUrl}}">{{activationUrl}}</a></p>
                <p>Se o link não abrir, use este token: <code>{{activationToken}}</code></p>
                <p>O convite expira em {{expiresAtUtc}}.</p>
                """,
            TextBody = """
                Você recebeu acesso administrativo ao painel central.

                Ative o seu acesso:
                {{activationUrl}}

                Token: {{activationToken}}
                Expira em: {{expiresAtUtc}}
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _emailTemplates.InsertAsync(template, cancellationToken);
    }

    private async Task SeedTenantOnboardingEmailTemplateAsync(
        Tenant tenant,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = TenantOnboardingTemplateKey,
            DisplayName = "Onboarding do tenant",
            Subject = "Tenant criado - {{tenantName}}",
            HtmlBody = """
                <p>Olá,</p>
                <p>O tenant <strong>{{tenantName}}</strong> foi criado com sucesso.</p>
                <p>Guarde as credenciais abaixo em local seguro; os segredos não serão reenviados.</p>
                <ul>
                  <li><strong>Tenant ID:</strong> {{tenantId}}</li>
                  <li><strong>Client ID:</strong> {{clientId}}</li>
                  <li><strong>Client Secret:</strong> {{clientSecret}}</li>
                </ul>
                <p>Ative o acesso administrativo pelo link abaixo:</p>
                <p><a href="{{activationUrl}}">{{activationUrl}}</a></p>
                <p>Token de ativação: <code>{{activationToken}}</code></p>
                <p>Expira em: {{expiresAtUtc}}</p>
                """,
            TextBody = """
                O tenant {{tenantName}} foi criado com sucesso.

                Guarde as credenciais abaixo em local seguro; os segredos não serão reenviados.

                Tenant ID: {{tenantId}}
                Client ID: {{clientId}}
                Client Secret: {{clientSecret}}
                Ativação administrativa:
                {{activationUrl}}

                Token de ativação: {{activationToken}}
                Expira em: {{expiresAtUtc}}
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _emailTemplates.InsertAsync(template, cancellationToken);
    }

    private async Task SeedEmailConfirmationTemplatesAsync(
        Tenant tenant,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var codeTemplate = new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = AccountEmailConfirmationFields.CodeTemplateKey,
            DisplayName = "Confirmação de email (código)",
            Subject = "Confirme o seu email — {{username}}",
            HtmlBody = """
                <p>Olá <strong>{{username}}</strong>,</p>
                <p>O seu código de confirmação é: <strong>{{code}}</strong></p>
                <p>O código expira em 30 minutos.</p>
                """,
            TextBody = """
                Olá {{username}},

                O seu código de confirmação é: {{code}}

                O código expira em 30 minutos.
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var linkTemplate = new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = AccountEmailConfirmationFields.LinkTemplateKey,
            DisplayName = "Confirmação de email (link)",
            Subject = "Confirme o seu email — {{username}}",
            HtmlBody = """
                <p>Olá <strong>{{username}}</strong>,</p>
                <p>Clique no link para confirmar o seu email:</p>
                <p><a href="{{confirmationLink}}">{{confirmationLink}}</a></p>
                <p>O link expira em 30 minutos.</p>
                """,
            TextBody = """
                Olá {{username}},

                Confirme o seu email através do link:
                {{confirmationLink}}

                O link expira em 30 minutos.
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _emailTemplates.InsertAsync(codeTemplate, cancellationToken);
        await _emailTemplates.InsertAsync(linkTemplate, cancellationToken);
    }

    private async Task SeedPasswordResetEmailTemplateAsync(
        Tenant tenant,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            Key = AccountPasswordResetFields.TemplateKey,
            DisplayName = "Redefinição de senha",
            Subject = "Redefina a sua senha",
            HtmlBody = """
                <p>Olá,</p>
                <p>Recebemos um pedido para redefinir a sua senha.</p>
                <p>Clique no link abaixo para escolher uma nova senha:</p>
                <p><a href="{{resetLink}}">{{resetLink}}</a></p>
                <p>O link expira em 60 minutos. Se não fez este pedido, ignore este email.</p>
                """,
            TextBody = """
                Recebemos um pedido para redefinir a sua senha.

                Aceda ao link:
                {{resetLink}}

                O link expira em 60 minutos. Se não fez este pedido, ignore este email.
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _emailTemplates.InsertAsync(template, cancellationToken);
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
