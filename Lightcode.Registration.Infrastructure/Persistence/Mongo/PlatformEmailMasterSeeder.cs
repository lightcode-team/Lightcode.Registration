using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Emails;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class PlatformEmailMasterSeedHostedService(
    IServiceProvider serviceProvider,
    ILogger<PlatformEmailMasterSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var mongoClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();
        var mongoOptions = scope.ServiceProvider.GetRequiredService<IOptions<MongoOptions>>().Value;
        var smtpOptions = scope.ServiceProvider.GetRequiredService<IOptions<MasterSmtpOptions>>().Value;
        var templateRepository = scope.ServiceProvider.GetRequiredService<IPlatformEmailTemplateRepository>();

        await UpsertMasterSmtpSettingsAsync(mongoClient, mongoOptions, smtpOptions, cancellationToken);
        await SeedMasterTemplatesAsync(templateRepository, cancellationToken);

        logger.LogInformation("Seeds de email da plataforma verificados no banco master {DatabaseName}.", mongoOptions.MasterDatabaseName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task UpsertMasterSmtpSettingsAsync(
        IMongoClient mongoClient,
        MongoOptions mongoOptions,
        MasterSmtpOptions smtpOptions,
        CancellationToken cancellationToken)
    {
        var from = string.IsNullOrWhiteSpace(smtpOptions.EmailRemetente)
            ? smtpOptions.Usuario
            : smtpOptions.EmailRemetente;

        var doc = new PlatformSmtpSettingsRoot
        {
            Id = PlatformSmtpSettingsRoot.DocumentId,
            Smtp = new TenantSmtpConfiguration
            {
                Host = smtpOptions.Host,
                Port = smtpOptions.Port,
                Usuario = smtpOptions.Usuario,
                Senha = smtpOptions.Senha,
                EmailRemetente = from,
                NomeRemetente = smtpOptions.NomeRemetente,
                UsarSsl = smtpOptions.UsarSsl
            }
        };

        var coll = mongoClient
            .GetDatabase(mongoOptions.MasterDatabaseName)
            .GetCollection<PlatformSmtpSettingsRoot>(PlatformSmtpSettingsRoot.CollectionName);

        await coll.ReplaceOneAsync(
            x => x.Id == PlatformSmtpSettingsRoot.DocumentId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private static async Task SeedMasterTemplatesAsync(
        IPlatformEmailTemplateRepository repository,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var template in BuildDefaultTemplates(now))
            await repository.InsertIfMissingAsync(template, cancellationToken);
    }

    private static IEnumerable<EmailTemplate> BuildDefaultTemplates(DateTime now)
    {
        yield return new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = PlatformEmailTemplates.TenantId,
            Key = PlatformEmailTemplates.PlatformAdminInvite,
            DisplayName = "Convite ADM central",
            Subject = "Convite de administracao - {{tenantName}}",
            HtmlBody = """
                <p>Ola,</p>
                <p>Voce recebeu acesso administrativo ao painel central.</p>
                <p>Ative o seu acesso pelo link abaixo:</p>
                <p><a href="{{activationUrl}}">{{activationUrl}}</a></p>
                <p>Se o link nao abrir, use este token: <code>{{activationToken}}</code></p>
                <p>O convite expira em {{expiresAtUtc}}.</p>
                """,
            TextBody = """
                Voce recebeu acesso administrativo ao painel central.

                Ative o seu acesso:
                {{activationUrl}}

                Token: {{activationToken}}
                Expira em: {{expiresAtUtc}}
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        yield return new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = PlatformEmailTemplates.TenantId,
            Key = PlatformEmailTemplates.TenantOnboarding,
            DisplayName = "Onboarding do tenant",
            Subject = "Tenant criado - {{tenantName}}",
            HtmlBody = """
                <p>Ola,</p>
                <p>O tenant <strong>{{tenantName}}</strong> foi criado com sucesso.</p>
                <p>Guarde as credenciais abaixo em local seguro; os segredos nao serao reenviados.</p>
                <ul>
                  <li><strong>Tenant ID:</strong> {{tenantId}}</li>
                  <li><strong>Client ID:</strong> {{clientId}}</li>
                  <li><strong>Client Secret:</strong> {{clientSecret}}</li>
                </ul>
                <p>Ative o acesso administrativo pelo link abaixo:</p>
                <p><a href="{{activationUrl}}">{{activationUrl}}</a></p>
                <p>Token de ativacao: <code>{{activationToken}}</code></p>
                <p>Expira em: {{expiresAtUtc}}</p>
                """,
            TextBody = """
                O tenant {{tenantName}} foi criado com sucesso.

                Guarde as credenciais abaixo em local seguro; os segredos nao serao reenviados.

                Tenant ID: {{tenantId}}
                Client ID: {{clientId}}
                Client Secret: {{clientSecret}}
                Ativacao administrativa:
                {{activationUrl}}

                Token de ativacao: {{activationToken}}
                Expira em: {{expiresAtUtc}}
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        yield return new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = PlatformEmailTemplates.TenantId,
            Key = PlatformEmailTemplates.PlatformAdminTwoFactorCode,
            DisplayName = "Codigo 2FA ADM central",
            Subject = "Codigo de verificacao Lightcode",
            HtmlBody = """
                <p>Ola, <strong>{{username}}</strong>.</p>
                <p>Seu codigo de verificacao Lightcode e: <strong>{{code}}</strong></p>
                <p>Finalidade: {{purpose}}</p>
                <p>O codigo expira em poucos minutos. Se voce nao solicitou esta acao, ignore este e-mail.</p>
                """,
            TextBody = """
                Ola, {{username}}.

                Seu codigo de verificacao Lightcode e: {{code}}

                Finalidade: {{purpose}}
                O codigo expira em poucos minutos. Se voce nao solicitou esta acao, ignore este e-mail.
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
