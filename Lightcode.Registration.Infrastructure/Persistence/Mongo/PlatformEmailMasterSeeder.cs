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
            Subject = "Convite de administração - {{tenantName}}",
            HtmlBody = PlatformEmailTemplateLayout.Build(
                preheader: "Você recebeu acesso administrativo ao painel central.",
                eyebrow: "Acesso administrativo",
                title: "Convite de administração",
                bodyHtml: """
                    <p style="margin: 0 0 16px 0;">Olá,</p>
                    <p style="margin: 0 0 16px 0;">Você recebeu acesso administrativo ao painel central para <strong>{{tenantName}}</strong>.</p>
                    <p style="margin: 0 0 24px 0;">Use o botão abaixo para ativar o seu acesso.</p>
                    """,
                actionUrl: "{{activationUrl}}",
                actionLabel: "Ativar acesso",
                secondaryNoteHtml: """
                    Se o botão não abrir, copie este token: <strong>{{activationToken}}</strong><br>
                    O convite expira em {{expiresAtUtc}}.
                    """),
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

        yield return new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = PlatformEmailTemplates.TenantId,
            Key = PlatformEmailTemplates.TenantOnboarding,
            DisplayName = "Onboarding do tenant",
            Subject = "Tenant criado - {{tenantName}}",
            HtmlBody = PlatformEmailTemplateLayout.Build(
                preheader: "O tenant {{tenantName}} foi criado com sucesso.",
                eyebrow: "Tenant criado",
                title: "Credenciais iniciais",
                bodyHtml: """
                    <p style="margin: 0 0 16px 0;">Olá,</p>
                    <p style="margin: 0 0 16px 0;">O tenant <strong>{{tenantName}}</strong> foi criado com sucesso.</p>
                    <p style="margin: 0 0 16px 0; color: #64748b;">Guarde as credenciais abaixo em local seguro; os segredos não serão reenviados.</p>
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin: 0 0 24px 0; border: 1px solid #e1e7ef; border-radius: 8px; background: #f8fafc;">
                      <tr>
                        <td style="padding: 12px 16px; color: #64748b; font-family: Arial, sans-serif; font-size: 13px; line-height: 18px;">Tenant ID</td>
                        <td style="padding: 12px 16px; color: #172033; font-family: Arial, sans-serif; font-size: 13px; font-weight: 700; line-height: 18px;">{{tenantId}}</td>
                      </tr>
                      <tr>
                        <td style="padding: 12px 16px; border-top: 1px solid #e1e7ef; color: #64748b; font-family: Arial, sans-serif; font-size: 13px; line-height: 18px;">Client ID</td>
                        <td style="padding: 12px 16px; border-top: 1px solid #e1e7ef; color: #172033; font-family: Arial, sans-serif; font-size: 13px; font-weight: 700; line-height: 18px;">{{clientId}}</td>
                      </tr>
                      <tr>
                        <td style="padding: 12px 16px; border-top: 1px solid #e1e7ef; color: #64748b; font-family: Arial, sans-serif; font-size: 13px; line-height: 18px;">Client Secret</td>
                        <td style="padding: 12px 16px; border-top: 1px solid #e1e7ef; color: #172033; font-family: Arial, sans-serif; font-size: 13px; font-weight: 700; line-height: 18px;">{{clientSecret}}</td>
                      </tr>
                    </table>
                    """,
                actionUrl: "{{activationUrl}}",
                actionLabel: "Ativar acesso administrativo",
                secondaryNoteHtml: """
                    Token de ativação: <strong>{{activationToken}}</strong><br>
                    Expira em {{expiresAtUtc}}.
                    """),
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

        yield return new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = PlatformEmailTemplates.TenantId,
            Key = PlatformEmailTemplates.PlatformAdminTwoFactorCode,
            DisplayName = "Código 2FA ADM central",
            Subject = "Código de verificação Lightcode",
            HtmlBody = PlatformEmailTemplateLayout.Build(
                preheader: "Use o código de verificação para continuar.",
                eyebrow: "Verificação em duas etapas",
                title: "Código de verificação",
                bodyHtml: """
                    <p style="margin: 0 0 16px 0;">Olá, <strong>{{username}}</strong>.</p>
                    <p style="margin: 0 0 16px 0;">Use o código abaixo para continuar:</p>
                    <div style="margin: 0 0 18px 0; padding: 16px; border: 1px solid #e1e7ef; border-radius: 8px; background: #f8fafc; color: #172033; font-family: Arial, sans-serif; font-size: 28px; font-weight: 800; letter-spacing: 4px; line-height: 32px; text-align: center;">
                      {{code}}
                    </div>
                    <p style="margin: 0 0 8px 0; color: #64748b;">Finalidade: <strong>{{purpose}}</strong></p>
                    """,
                secondaryNoteHtml: "O código expira em poucos minutos. Se você não solicitou esta ação, ignore este e-mail."),
            TextBody = """
                Olá, {{username}}.

                Seu código de verificação Lightcode é: {{code}}

                Finalidade: {{purpose}}
                O código expira em poucos minutos. Se você não solicitou esta ação, ignore este e-mail.
                """,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
