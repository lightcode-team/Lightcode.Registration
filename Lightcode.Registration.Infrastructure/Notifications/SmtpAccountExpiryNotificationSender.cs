using System.Net;
using System.Net.Mail;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Expiry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Infrastructure.Notifications;

public sealed class SmtpAccountExpiryNotificationSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpAccountExpiryNotificationSender> logger) : IAccountExpiryNotificationSender
{
    public async Task SendExpiryReminderAsync(RegistrationExpiryReminderMessage message, CancellationToken cancellationToken = default)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.UseSsl,
            Credentials = string.IsNullOrEmpty(o.User)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(o.User, o.Password)
        };

        var subject = message.ReminderKind == 30
            ? "O seu cadastro expira em breve (30 dias)"
            : "O seu cadastro expira em breve (15 dias)";

        var body =
            $"O seu registo irá expirar em {message.RegistrationExpiresAtUtc:u} (UTC). " +
            "Atualize os dados da sua conta para renovar o cadastro.";

        using var mail = new MailMessage
        {
            From = new MailAddress(o.FromAddress, o.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        mail.To.Add(message.Email);

        await client.SendMailAsync(mail, cancellationToken);
        logger.LogInformation("Email de lembrete {Kind}d enviado para {Email}", message.ReminderKind, message.Email);
    }
}
