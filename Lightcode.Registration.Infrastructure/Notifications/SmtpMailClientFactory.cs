using System.Net;
using System.Net.Mail;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Infrastructure.Notifications;

internal static class SmtpMailClientFactory
{
    public static (SmtpClient Client, MailMessage Mail) CreateForSend(
        TenantSmtpConfiguration smtp,
        string to,
        string subject,
        string body,
        bool isBodyHtml)
    {
        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException("SMTP do tenant: Host em falta.");

        if (string.IsNullOrWhiteSpace(smtp.EmailRemetente))
            throw new InvalidOperationException("SMTP do tenant: EmailRemetente em falta.");

        // Portas de submissão (587 STARTTLS, 465 SMTPS, 2525 alternativa) exigem negociação TLS;
        // sem EnableSsl o Gmail e a maioria dos relays públicos respondem "Must issue a STARTTLS command first".
        var submissionPort = smtp.Port is 587 or 465 or 2525;
        var enableSsl = smtp.UsarSsl || submissionPort;

        var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = enableSsl,
            Credentials = string.IsNullOrEmpty(smtp.Usuario)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(smtp.Usuario, smtp.Senha)
        };

        var mail = new MailMessage
        {
            From = new MailAddress(smtp.EmailRemetente, string.IsNullOrWhiteSpace(smtp.NomeRemetente) ? smtp.EmailRemetente : smtp.NomeRemetente),
            Subject = subject,
            Body = body,
            IsBodyHtml = isBodyHtml
        };
        mail.To.Add(to);

        return (client, mail);
    }
}
