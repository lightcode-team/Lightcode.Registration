using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Emails.Commands;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Handlers;

public sealed class SendEmailCommandHandler(
    IEmailTemplateRepository repository,
    IEmailEnqueuePublisher enqueuePublisher) : IRequestHandler<SendEmailCommand, ServiceResult<SendEmailQueuedDto>>
{
    public async Task<ServiceResult<SendEmailQueuedDto>> Handle(SendEmailCommand request, CancellationToken cancellationToken)
    {
        var body = request.Body;
        if (string.IsNullOrWhiteSpace(body.To))
            return ServiceResult<SendEmailQueuedDto>.Fail(400, "Destinatário (To) é obrigatório.");

        var hasId = !string.IsNullOrWhiteSpace(body.TemplateId);
        var hasKey = !string.IsNullOrWhiteSpace(body.TemplateKey);
        if (hasId == hasKey)
            return ServiceResult<SendEmailQueuedDto>.Fail(400, "Envie exatamente um dos campos: templateId ou templateKey.");

        if (hasId)
        {
            var template = await repository.GetByIdAsync(request.TenantId, body.TemplateId!.Trim(), cancellationToken);
            if (template is null)
                return ServiceResult<SendEmailQueuedDto>.Fail(404, "Template não encontrado.");
        }
        else
        {
            var template = await repository.GetByKeyAsync(request.TenantId, body.TemplateKey!.Trim(), cancellationToken);
            if (template is null)
                return ServiceResult<SendEmailQueuedDto>.Fail(404, "Template não encontrado.");
        }

        var message = new EmailDispatchQueueMessage(
            request.TenantId,
            hasId ? body.TemplateId!.Trim() : null,
            hasKey ? body.TemplateKey!.Trim() : null,
            body.To.Trim(),
            body.Parameters);

        var messageId = await enqueuePublisher.PublishSendAsync(message, cancellationToken);

        return ServiceResult<SendEmailQueuedDto>.Ok(
            new SendEmailQueuedDto(messageId),
            202,
            "Pedido de envio enfileirado.");
    }
}
