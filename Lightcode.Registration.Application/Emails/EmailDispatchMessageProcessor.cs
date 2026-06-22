using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Emails;

public sealed class EmailDispatchMessageProcessor(
    IEmailTemplateRepository tenantTemplateRepository,
    IPlatformEmailTemplateRepository platformTemplateRepository,
    IOutboundMailSender mailSender,
    ISystemOutboundMailSender systemMailSender)
{
    public async Task<bool> ProcessAsync(
        EmailDispatchQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.To))
            return false;

        return message.SystemEmail
            ? await SendSystemEmailAsync(message, cancellationToken)
            : await SendTenantEmailAsync(message, cancellationToken);
    }

    private async Task<bool> SendTenantEmailAsync(
        EmailDispatchQueueMessage message,
        CancellationToken cancellationToken)
    {
        EmailTemplate? template = null;
        if (!string.IsNullOrWhiteSpace(message.TemplateId))
            template = await tenantTemplateRepository.GetByIdAsync(message.TenantId, message.TemplateId, cancellationToken);
        else if (!string.IsNullOrWhiteSpace(message.TemplateKey))
            template = await tenantTemplateRepository.GetByKeyAsync(message.TenantId, message.TemplateKey, cancellationToken);

        if (template is null)
            return false;

        var subject = EmailTemplatePlaceholderMerger.Merge(template.Subject ?? string.Empty, message.Parameters);
        var html = EmailTemplatePlaceholderMerger.Merge(template.HtmlBody ?? string.Empty, message.Parameters);
        var text = template.TextBody is null
            ? null
            : EmailTemplatePlaceholderMerger.Merge(template.TextBody, message.Parameters);

        await mailSender.SendAsync(message.TenantId, message.To, subject, html, text, cancellationToken);
        return true;
    }

    private async Task<bool> SendSystemEmailAsync(
        EmailDispatchQueueMessage message,
        CancellationToken cancellationToken)
    {
        var subject = message.Subject;
        var htmlBody = message.HtmlBody;
        var textBody = message.TextBody;

        if (!string.IsNullOrWhiteSpace(message.TemplateId) || !string.IsNullOrWhiteSpace(message.TemplateKey))
        {
            EmailTemplate? template = null;
            if (!string.IsNullOrWhiteSpace(message.TemplateId))
                template = await platformTemplateRepository.GetByIdAsync(message.TemplateId, cancellationToken);
            else if (!string.IsNullOrWhiteSpace(message.TemplateKey))
                template = await platformTemplateRepository.GetByKeyAsync(message.TemplateKey, cancellationToken);

            if (template is null)
                return false;

            subject = EmailTemplatePlaceholderMerger.Merge(template.Subject ?? string.Empty, message.Parameters);
            htmlBody = EmailTemplatePlaceholderMerger.Merge(template.HtmlBody ?? string.Empty, message.Parameters);
            textBody = template.TextBody is null
                ? null
                : EmailTemplatePlaceholderMerger.Merge(template.TextBody, message.Parameters);
        }

        if (string.IsNullOrWhiteSpace(subject))
            return false;

        if (string.IsNullOrWhiteSpace(htmlBody) && string.IsNullOrWhiteSpace(textBody))
            return false;

        await systemMailSender.SendAsync(message.To, subject, htmlBody, textBody, cancellationToken);
        return true;
    }
}
