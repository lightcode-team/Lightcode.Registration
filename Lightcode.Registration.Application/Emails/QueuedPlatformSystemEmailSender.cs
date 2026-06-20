using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;

namespace Lightcode.Registration.Application.Emails;

public sealed class QueuedPlatformSystemEmailSender(IEmailEnqueuePublisher emailEnqueuePublisher)
    : IPlatformSystemEmailSender
{
    public Task SendTwoFactorCodeAsync(
        string to,
        string username,
        string code,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailDispatchQueueMessage(
            TenantId: "platform",
            TemplateId: null,
            TemplateKey: null,
            To: to,
            Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["username"] = username,
                ["purpose"] = purpose
            },
            SystemEmail: true,
            Subject: "Código de verificação Lightcode",
            TextBody: BuildBody(username, code, purpose));

        return emailEnqueuePublisher.PublishSendAsync(message, cancellationToken);
    }

    private static string BuildBody(string username, string code, string purpose) =>
        $"""
        Olá, {username}.

        Seu código de verificação Lightcode é: {code}

        Finalidade: {purpose}
        O código expira em poucos minutos. Se você não solicitou esta ação, ignore este e-mail.
        """;
}
