using Lightcode.Registration.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Infrastructure.Notifications;

public sealed class LoggingOutboundMailSender(ILogger<LoggingOutboundMailSender> logger) : IOutboundMailSender
{
    public Task SendAsync(
        string tenantId,
        string to,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Email simulado] Tenant={TenantId} To={To} Subject={Subject} HtmlLength={HtmlLen} TextLength={TextLen}",
            tenantId,
            to,
            subject,
            htmlBody?.Length ?? 0,
            textBody?.Length ?? 0);
        return Task.CompletedTask;
    }
}
