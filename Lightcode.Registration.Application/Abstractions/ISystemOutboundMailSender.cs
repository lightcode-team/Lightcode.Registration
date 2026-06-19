namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Envio de emails globais da plataforma ja compostos, usando SMTP master ou log.</summary>
public interface ISystemOutboundMailSender
{
    Task SendAsync(
        string to,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default);
}
