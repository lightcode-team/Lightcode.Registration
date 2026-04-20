using System.Text.Json;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Lightcode.Registration.Infrastructure.Email;

public sealed class RabbitMqEmailEnqueuePublisher(IConnection connection, ILogger<RabbitMqEmailEnqueuePublisher> logger)
    : IEmailEnqueuePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task<string> PublishSendAsync(EmailDispatchQueueMessage message, CancellationToken cancellationToken = default)
    {
        EmailDispatchRabbitTopology.Ensure(connection);

        using var channel = connection.CreateModel();
        var messageId = Guid.NewGuid().ToString("N");
        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = messageId;

        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        channel.BasicPublish(
            EmailDispatchRabbitTopology.ExchangeName,
            EmailDispatchRabbitTopology.RoutingKey,
            props,
            body);

        logger.LogInformation("Pedido de email enfileirado MessageId={MessageId} To={To}", messageId, message.To);
        return Task.FromResult(messageId);
    }
}
