using System.Net;
using System.Net.Mail;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Infrastructure.Email;

public sealed class SmtpPlatformSystemEmailSender(
    IOptions<MasterSmtpOptions> options,
    ILogger<SmtpPlatformSystemEmailSender> logger) : IPlatformSystemEmailSender
{
    public async Task SendTwoFactorCodeAsync(
        string to,
        string username,
        string code,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var smtp = options.Value;
        if (!smtp.UseSmtp)
        {
            logger.LogInformation(
                "2FA platform admin email skipped because MasterSmtp:UseSmtp is false. To={To}, Purpose={Purpose}",
                to,
                purpose);
            return;
        }

        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException("MasterSmtp:Host é obrigatório para enviar e-mail de sistema.");

        var from = string.IsNullOrWhiteSpace(smtp.EmailRemetente)
            ? smtp.Usuario
            : smtp.EmailRemetente;
        if (string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("MasterSmtp:EmailRemetente ou MasterSmtp:Usuario é obrigatório.");

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UsarSsl || smtp.Port is 587 or 465 or 2525
        };

        if (!string.IsNullOrWhiteSpace(smtp.Usuario))
            client.Credentials = new NetworkCredential(smtp.Usuario, smtp.Senha);

        using var mail = new MailMessage
        {
            From = new MailAddress(from, string.IsNullOrWhiteSpace(smtp.NomeRemetente) ? from : smtp.NomeRemetente),
            Subject = "Código de verificação Lightcode",
            Body = BuildBody(username, code, purpose),
            IsBodyHtml = false
        };
        mail.To.Add(to);

        await client.SendMailAsync(mail, cancellationToken);
        logger.LogInformation("2FA platform admin email sent. To={To}, Purpose={Purpose}", to, purpose);
    }

    private static string BuildBody(string username, string code, string purpose) =>
        $"""
        Olá, {username}.

        Seu código de verificação Lightcode é: {code}

        Finalidade: {purpose}
        O código expira em poucos minutos. Se você não solicitou esta ação, ignore este e-mail.
        """;
}
