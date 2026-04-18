using RabbitMQ.Client;

namespace Lightcode.Registration.Worker.RabbitMq;

public static class AccountRegistrationRabbitTopology
{
    public const string ExchangeName = "account.registration";

    public const string ReminderQueueName = "account.expiry.reminders";

    public static string ReminderRoutingKey(int reminderKind) => $"expiry.reminder.{reminderKind}";

    public static void Ensure(IConnection connection)
    {
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
        channel.QueueDeclare(ReminderQueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(ReminderQueueName, ExchangeName, "expiry.reminder.#");
    }
}
