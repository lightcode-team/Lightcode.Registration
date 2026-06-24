using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Application.Emails;

public sealed class EmailDispatchMessageProcessor(
    IEmailTemplateRepository tenantTemplateRepository,
    IPlatformEmailTemplateRepository platformTemplateRepository,
    IOutboundMailSender mailSender,
    ISystemOutboundMailSender systemMailSender,
    ILogger<EmailDispatchMessageProcessor> logger)
{
    public async Task<bool> ProcessAsync(
        EmailDispatchQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.To))
        {
            logger.LogWarning(
                "Mensagem de email descartada: destinatario vazio. TenantId={TenantId} TemplateKey={TemplateKey} SystemEmail={SystemEmail}",
                message.TenantId,
                message.TemplateKey,
                message.SystemEmail);
            return false;
        }

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
        {
            logger.LogWarning(
                "Template de email de tenant nao encontrado. TenantId={TenantId} TemplateId={TemplateId} TemplateKey={TemplateKey}",
                message.TenantId,
                message.TemplateId,
                message.TemplateKey);
            return false;
        }

        var subject = EmailTemplatePlaceholderMerger.Merge(template.Subject ?? string.Empty, message.Parameters);
        var html = EmailTemplatePlaceholderMerger.Merge(template.HtmlBody ?? string.Empty, message.Parameters);
        var text = template.TextBody is null
            ? null
            : EmailTemplatePlaceholderMerger.Merge(template.TextBody, message.Parameters);

        await mailSender.SendAsync(message.TenantId, message.To, subject, html, text, cancellationToken);
        logger.LogInformation(
            "Mensagem de email de tenant processada. TenantId={TenantId} TemplateId={TemplateId} TemplateKey={TemplateKey}",
            message.TenantId,
            message.TemplateId,
            message.TemplateKey);
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
            {
                logger.LogWarning(
                    "Template de email de sistema nao encontrado. TemplateId={TemplateId} TemplateKey={TemplateKey}",
                    message.TemplateId,
                    message.TemplateKey);
                return false;
            }

            subject = EmailTemplatePlaceholderMerger.Merge(template.Subject ?? string.Empty, message.Parameters);
            htmlBody = EmailTemplatePlaceholderMerger.Merge(template.HtmlBody ?? string.Empty, message.Parameters);
            textBody = template.TextBody is null
                ? null
                : EmailTemplatePlaceholderMerger.Merge(template.TextBody, message.Parameters);
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            logger.LogWarning(
                "Mensagem de email de sistema descartada: assunto vazio. TemplateId={TemplateId} TemplateKey={TemplateKey}",
                message.TemplateId,
                message.TemplateKey);
            return false;
        }

        if (string.IsNullOrWhiteSpace(htmlBody) && string.IsNullOrWhiteSpace(textBody))
        {
            logger.LogWarning(
                "Mensagem de email de sistema descartada: corpo vazio. TemplateId={TemplateId} TemplateKey={TemplateKey}",
                message.TemplateId,
                message.TemplateKey);
            return false;
        }

        await systemMailSender.SendAsync(message.To, subject, htmlBody, textBody, cancellationToken);
        logger.LogInformation(
            "Mensagem de email de sistema processada. TemplateId={TemplateId} TemplateKey={TemplateKey}",
            message.TemplateId,
            message.TemplateKey);
        return true;
    }
}
