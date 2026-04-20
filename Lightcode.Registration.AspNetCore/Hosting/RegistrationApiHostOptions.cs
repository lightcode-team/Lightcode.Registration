namespace Lightcode.Registration.AspNetCore.Hosting;

public sealed class RegistrationApiHostOptions
{
    /// <summary>Quando verdadeiro, regista ligação singleton ao RabbitMQ (publicação de mensagens, etc.).</summary>
    public bool RegisterRabbitMqConnection { get; set; }

    /// <summary>Nome do cliente na ligação AMQP (visível no management do RabbitMQ).</summary>
    public string RabbitMqConnectionClientName { get; set; } = "lightcode-registration-api";
}
