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
            TenantId: PlatformEmailTemplates.TenantId,
            TemplateId: null,
            TemplateKey: PlatformEmailTemplates.PlatformAdminTwoFactorCode,
            To: to,
            Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["username"] = username,
                ["code"] = code,
                ["purpose"] = purpose
            },
            SystemEmail: true);

        return emailEnqueuePublisher.PublishSendAsync(message, cancellationToken);
    }
}
