using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Infrastructure.Email;

/// <summary>Substituto quando este host não regista ligação singleton ao RabbitMQ (ex.: API de registo sem fila de email).</summary>
public sealed class DisabledEmailEnqueuePublisher(ILogger<DisabledEmailEnqueuePublisher> logger) : IEmailEnqueuePublisher
{
    public Task<string> PublishSendAsync(EmailDispatchQueueMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Tentativa de enfileirar email num host sem RabbitMQ configurado para publicação.");
        throw new InvalidOperationException(
            "O envio de email enfileirado não está disponível neste host. Utilize a API de email dedicada (EmailApi) com RabbitMQ.");
    }
}
