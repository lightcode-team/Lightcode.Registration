using Lightcode.Registration.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Infrastructure.Notifications;

public sealed class SmtpOutboundMailSender(IServiceScopeFactory scopeFactory, ILogger<SmtpOutboundMailSender> logger)
    : IOutboundMailSender
{
    public async Task SendAsync(
        string tenantId,
        string to,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITenantSmtpSettingsRepository>();
        var smtp = await repo.GetSmtpAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Configuração SMTP do tenant '{tenantId}' não encontrada (collection Settings, _id=smtp).");

        var isHtml = !string.IsNullOrEmpty(htmlBody);
        var body = isHtml ? htmlBody! : (textBody ?? string.Empty);

        var (client, mail) = SmtpMailClientFactory.CreateForSend(smtp, to, subject, body, isHtml);
        using (client)
        using (mail)
            await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Email enviado via SMTP (tenant {TenantId}) para {To}", tenantId, to);
    }
}
