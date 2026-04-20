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

        var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UsarSsl,
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
