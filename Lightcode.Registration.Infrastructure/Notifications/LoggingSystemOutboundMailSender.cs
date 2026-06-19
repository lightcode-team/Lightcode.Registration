using Lightcode.Registration.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Infrastructure.Notifications;

public sealed class LoggingSystemOutboundMailSender(ILogger<LoggingSystemOutboundMailSender> logger)
    : ISystemOutboundMailSender
{
    public Task SendAsync(
        string to,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Email de sistema simulado] To={To} Subject={Subject} HtmlLength={HtmlLen} TextLength={TextLen}",
            to,
            subject,
            htmlBody?.Length ?? 0,
            textBody?.Length ?? 0);
        return Task.CompletedTask;
    }
}
