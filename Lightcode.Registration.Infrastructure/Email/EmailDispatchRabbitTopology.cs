using RabbitMQ.Client;

namespace Lightcode.Registration.Infrastructure.Email;

public static class EmailDispatchRabbitTopology
{
    public const string ExchangeName = "email.outbound";

    public const string SendQueueName = "email.send";

    public const string RoutingKey = "email.send.request";

    public static void Ensure(IConnection connection)
    {
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
        channel.QueueDeclare(SendQueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(SendQueueName, ExchangeName, RoutingKey);
    }
}
