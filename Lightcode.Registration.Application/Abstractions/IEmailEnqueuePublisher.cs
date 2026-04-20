using Lightcode.Registration.Application.Contracts.Email;

namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Publica pedido de envio de email na fila RabbitMQ.</summary>
public interface IEmailEnqueuePublisher
{
    /// <returns>Identificador da mensagem (ex.: para correlação).</returns>
    Task<string> PublishSendAsync(EmailDispatchQueueMessage message, CancellationToken cancellationToken = default);
}
