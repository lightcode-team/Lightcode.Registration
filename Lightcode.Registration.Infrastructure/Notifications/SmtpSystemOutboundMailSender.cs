using System.Net;
using System.Net.Mail;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Infrastructure.Notifications;

public sealed class SmtpSystemOutboundMailSender(
    IOptions<MasterSmtpOptions> options,
    ILogger<SmtpSystemOutboundMailSender> logger) : ISystemOutboundMailSender
{
    public async Task SendAsync(
        string to,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException("MasterSmtp:Host e obrigatorio para enviar e-mail de sistema.");

        var from = string.IsNullOrWhiteSpace(smtp.EmailRemetente)
            ? smtp.Usuario
            : smtp.EmailRemetente;
        if (string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("MasterSmtp:EmailRemetente ou MasterSmtp:Usuario e obrigatorio.");

        var isHtml = !string.IsNullOrWhiteSpace(htmlBody);
        var body = isHtml ? htmlBody! : (textBody ?? string.Empty);

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UsarSsl || smtp.Port is 587 or 465 or 2525
        };

        if (!string.IsNullOrWhiteSpace(smtp.Usuario))
            client.Credentials = new NetworkCredential(smtp.Usuario, smtp.Senha);

        using var mail = new MailMessage
        {
            From = new MailAddress(from, string.IsNullOrWhiteSpace(smtp.NomeRemetente) ? from : smtp.NomeRemetente),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        mail.To.Add(to);

        await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Email de sistema enviado via SMTP master para {To}", to);
    }
}
