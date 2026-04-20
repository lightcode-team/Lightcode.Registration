using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Expiry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Infrastructure.Notifications;

public sealed class SmtpAccountExpiryNotificationSender(
    IServiceScopeFactory scopeFactory,
    ILogger<SmtpAccountExpiryNotificationSender> logger) : IAccountExpiryNotificationSender
{
    public async Task SendExpiryReminderAsync(RegistrationExpiryReminderMessage message, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITenantSmtpSettingsRepository>();
        var smtp = await repo.GetSmtpAsync(message.TenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Configuração SMTP do tenant '{message.TenantId}' não encontrada.");

        var subject = message.ReminderKind == 30
            ? "O seu cadastro expira em breve (30 dias)"
            : "O seu cadastro expira em breve (15 dias)";

        var body =
            $"O seu registo irá expirar em {message.RegistrationExpiresAtUtc:u} (UTC). " +
            "Atualize os dados da sua conta para renovar o cadastro.";

        var (client, mail) = SmtpMailClientFactory.CreateForSend(smtp, message.Email, subject, body, isBodyHtml: false);
        using (client)
        using (mail)
            await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Email de lembrete {Kind}d enviado para {Email} (tenant {TenantId})", message.ReminderKind, message.Email, message.TenantId);
    }
}
