namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Envio SMTP (ou log) do email já composto; credenciais vêm da configuração SMTP do tenant.</summary>
public interface IOutboundMailSender
{
    Task SendAsync(
        string tenantId,
        string to,
        string subject,
        string? htmlBody,
        string? textBody,
        CancellationToken cancellationToken = default);
}
