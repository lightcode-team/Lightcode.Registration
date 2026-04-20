using System.Text;
using System.Text.Json;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Expiry;
using Lightcode.Registration.Worker.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Lightcode.Registration.Worker;

public sealed class RegistrationExpiryReminderConsumerHostedService(
    IConnection rabbitConnection,
    IAccountExpiryNotificationSender notificationSender,
    IServiceScopeFactory scopeFactory,
    ILogger<RegistrationExpiryReminderConsumerHostedService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AccountRegistrationRabbitTopology.Ensure(rabbitConnection);

        await Task.Yield();

        var channel = rabbitConnection.CreateModel();
        channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var message = JsonSerializer.Deserialize<RegistrationExpiryReminderMessage>(json, JsonOptions);
                if (message is null)
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                await notificationSender.SendExpiryReminderAsync(message, stoppingToken);

                var sentUtc = DateTime.UtcNow;
                using (var scope = scopeFactory.CreateScope())
                {
                    var userAccountWriter = scope.ServiceProvider.GetRequiredService<IUserAccountWriter>();
                    await userAccountWriter.MarkExpiryReminderSentAsync(
                        message.TenantId,
                        message.UserId,
                        message.ReminderKind,
                        sentUtc,
                        stoppingToken);
                }

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao processar mensagem de lembrete de expiração.");
                channel.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
        };

        channel.BasicConsume(AccountRegistrationRabbitTopology.ReminderQueueName, autoAck: false, consumer: consumer);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
