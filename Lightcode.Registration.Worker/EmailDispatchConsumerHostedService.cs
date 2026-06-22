using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Emails;
using Lightcode.Registration.Domain.Entities;
using Lightcode.Registration.Infrastructure.Email;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Lightcode.Registration.Worker;

public sealed class EmailDispatchConsumerHostedService(
    IConnection rabbitConnection,
    IServiceScopeFactory scopeFactory,
    IOutboundMailSender mailSender,
    ISystemOutboundMailSender systemMailSender,
    ILogger<EmailDispatchConsumerHostedService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EmailDispatchRabbitTopology.Ensure(rabbitConnection);

        await Task.Yield();

        var channel = rabbitConnection.CreateModel();
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var message = JsonSerializer.Deserialize<EmailDispatchQueueMessage>(json, JsonOptions);
                if (message is null || string.IsNullOrWhiteSpace(message.To))
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                using var scope = scopeFactory.CreateScope();

                if (message.SystemEmail)
                {
                    await SendSystemEmailAsync(message, scope.ServiceProvider, stoppingToken);
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var repository = scope.ServiceProvider.GetRequiredService<IEmailTemplateRepository>();

                EmailTemplate? template = null;
                if (!string.IsNullOrWhiteSpace(message.TemplateId))
                    template = await repository.GetByIdAsync(message.TenantId, message.TemplateId, stoppingToken);
                else if (!string.IsNullOrWhiteSpace(message.TemplateKey))
                    template = await repository.GetByKeyAsync(message.TenantId, message.TemplateKey, stoppingToken);

                if (template is null)
                {
                    logger.LogWarning("Template não encontrado para envio (tenant={TenantId}).", message.TenantId);
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var t = template;
                var subject = EmailTemplatePlaceholderMerger.Merge(t.Subject ?? string.Empty, message.Parameters);
                var html = EmailTemplatePlaceholderMerger.Merge(t.HtmlBody ?? string.Empty, message.Parameters);
                var text = t.TextBody is null
                    ? null
                    : EmailTemplatePlaceholderMerger.Merge(t.TextBody, message.Parameters);

                await mailSender.SendAsync(message.TenantId, message.To, subject, html, text, stoppingToken);
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar mensagem de envio de email.");
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        channel.BasicConsume(EmailDispatchRabbitTopology.SendQueueName, autoAck: false, consumer: consumer);

        // Manter ExecuteAsync ativo até cancelamento — igual a RegistrationExpiryReminderConsumerHostedService.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // encerramento
        }
    }

    private async Task SendSystemEmailAsync(
        EmailDispatchQueueMessage message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var subject = message.Subject;
        var htmlBody = message.HtmlBody;
        var textBody = message.TextBody;

        if (!string.IsNullOrWhiteSpace(message.TemplateId) || !string.IsNullOrWhiteSpace(message.TemplateKey))
        {
            var repository = serviceProvider.GetRequiredService<IPlatformEmailTemplateRepository>();

            EmailTemplate? template = null;
            if (!string.IsNullOrWhiteSpace(message.TemplateId))
                template = await repository.GetByIdAsync(message.TemplateId, cancellationToken);
            else if (!string.IsNullOrWhiteSpace(message.TemplateKey))
                template = await repository.GetByKeyAsync(message.TemplateKey, cancellationToken);

            if (template is null)
            {
                logger.LogWarning(
                    "Template master nao encontrado para envio de sistema (templateId={TemplateId}, templateKey={TemplateKey}).",
                    message.TemplateId,
                    message.TemplateKey);
                return;
            }

            subject = EmailTemplatePlaceholderMerger.Merge(template.Subject ?? string.Empty, message.Parameters);
            htmlBody = EmailTemplatePlaceholderMerger.Merge(template.HtmlBody ?? string.Empty, message.Parameters);
            textBody = template.TextBody is null
                ? null
                : EmailTemplatePlaceholderMerger.Merge(template.TextBody, message.Parameters);
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            logger.LogWarning("Mensagem de email de sistema sem assunto para {To}.", message.To);
            return;
        }

        if (string.IsNullOrWhiteSpace(htmlBody) && string.IsNullOrWhiteSpace(textBody))
        {
            logger.LogWarning("Mensagem de email de sistema sem corpo para {To}.", message.To);
            return;
        }

        await systemMailSender.SendAsync(
            message.To,
            subject,
            htmlBody,
            textBody,
            cancellationToken);
    }
}
