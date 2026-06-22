using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Emails;
using Lightcode.Registration.Infrastructure.Email;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Lightcode.Registration.Worker;

public sealed class EmailDispatchConsumerHostedService(
    IConnection rabbitConnection,
    IServiceScopeFactory scopeFactory,
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
                var processor = scope.ServiceProvider.GetRequiredService<EmailDispatchMessageProcessor>();
                if (!await processor.ProcessAsync(message, stoppingToken))
                {
                    logger.LogWarning(
                        "Mensagem de email ignorada por dados insuficientes ou template ausente (tenant={TenantId}, system={SystemEmail}, templateId={TemplateId}, templateKey={TemplateKey}).",
                        message.TenantId,
                        message.SystemEmail,
                        message.TemplateId,
                        message.TemplateKey);
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar mensagem de envio de email.");
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        channel.BasicConsume(EmailDispatchRabbitTopology.SendQueueName, autoAck: false, consumer: consumer);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Encerramento normal.
        }
    }
}
