using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.TwoFactor;

public sealed class EmailCodeTwoFactorMethod(
    IEmailEnqueuePublisher emailEnqueuePublisher,
    IPlatformSystemEmailSender platformSystemEmailSender) : ITwoFactorMethod
{
    public const string TemplateKey = "account-login-2fa-code";

    public string Method => TwoFactorMethods.EmailCode;

    public async Task SendAsync(
        TwoFactorChallengeSubject subject,
        string code,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject.TenantId))
        {
            if (!string.Equals(subject.SubjectType, TwoFactorSubjectTypes.PlatformAdmin, StringComparison.Ordinal))
                throw new InvalidOperationException("2FA por e-mail de usuário final exige tenant.");

            await platformSystemEmailSender.SendTwoFactorCodeAsync(
                subject.Email,
                subject.Username,
                code,
                purpose,
                cancellationToken);
            return;
        }

        await emailEnqueuePublisher.PublishSendAsync(
            new EmailDispatchQueueMessage(
                subject.TenantId,
                TemplateId: null,
                TemplateKey: TemplateKey,
                To: subject.Email,
                Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["username"] = subject.Username,
                    ["code"] = code,
                    ["purpose"] = purpose
                }),
            cancellationToken);
    }
}
