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
            Subject: "Codigo de verificacao Lightcode",
            TextBody: BuildBody(username, code, purpose));

        return emailEnqueuePublisher.PublishSendAsync(message, cancellationToken);
    }

    private static string BuildBody(string username, string code, string purpose) =>
        $"""
        Ola, {username}.

        Seu codigo de verificacao Lightcode e: {code}

        Finalidade: {purpose}
        O codigo expira em poucos minutos. Se voce nao solicitou esta acao, ignore este e-mail.
        """;
}
