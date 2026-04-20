using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Emails.Commands;
using Lightcode.Registration.Application.Emails.Queries;
using Lightcode.Registration.Domain.Entities;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Handlers;

public sealed class ListEmailTemplatesQueryHandler(IEmailTemplateRepository repository)
    : IRequestHandler<ListEmailTemplatesQuery, ServiceResult<IReadOnlyList<EmailTemplateDto>>>
{
    public async Task<ServiceResult<IReadOnlyList<EmailTemplateDto>>> Handle(
        ListEmailTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var list = await repository.ListByTenantAsync(request.TenantId, cancellationToken);
        return ServiceResult<IReadOnlyList<EmailTemplateDto>>.Ok(list.Select(Map).ToList());
    }

    private static EmailTemplateDto Map(EmailTemplate e) =>
        new(e.Id, e.TenantId, e.Key, e.DisplayName, e.Subject, e.HtmlBody, e.TextBody, e.CreatedAtUtc, e.UpdatedAtUtc);
}

public sealed class GetEmailTemplateByIdQueryHandler(IEmailTemplateRepository repository)
    : IRequestHandler<GetEmailTemplateByIdQuery, ServiceResult<EmailTemplateDto>>
{
    public async Task<ServiceResult<EmailTemplateDto>> Handle(GetEmailTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
        if (entity is null)
            return ServiceResult<EmailTemplateDto>.Fail(404, "Template não encontrado.");

        return ServiceResult<EmailTemplateDto>.Ok(Map(entity));
    }

    private static EmailTemplateDto Map(EmailTemplate e) =>
        new(e.Id, e.TenantId, e.Key, e.DisplayName, e.Subject, e.HtmlBody, e.TextBody, e.CreatedAtUtc, e.UpdatedAtUtc);
}

public sealed class CreateEmailTemplateCommandHandler(IEmailTemplateRepository repository)
    : IRequestHandler<CreateEmailTemplateCommand, ServiceResult<EmailTemplateDto>>
{
    public async Task<ServiceResult<EmailTemplateDto>> Handle(CreateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var body = request.Body;
        if (string.IsNullOrWhiteSpace(body.Key))
            return ServiceResult<EmailTemplateDto>.Fail(400, "Key é obrigatória.");
        if (string.IsNullOrWhiteSpace(body.Subject))
            return ServiceResult<EmailTemplateDto>.Fail(400, "Subject é obrigatório.");
        if (string.IsNullOrWhiteSpace(body.HtmlBody))
            return ServiceResult<EmailTemplateDto>.Fail(400, "HtmlBody é obrigatório.");

        var key = body.Key.Trim();
        var existing = await repository.GetByKeyAsync(request.TenantId, key, cancellationToken);
        if (existing is not null)
            return ServiceResult<EmailTemplateDto>.Fail(409, "Já existe um template com esta Key.");

        var now = DateTime.UtcNow;
        var entity = new EmailTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = request.TenantId,
            Key = key,
            DisplayName = string.IsNullOrWhiteSpace(body.DisplayName) ? null : body.DisplayName.Trim(),
            Subject = body.Subject.Trim(),
            HtmlBody = body.HtmlBody,
            TextBody = string.IsNullOrWhiteSpace(body.TextBody) ? null : body.TextBody,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.InsertAsync(entity, cancellationToken);
        return ServiceResult<EmailTemplateDto>.Ok(Map(entity), 201);
    }

    private static EmailTemplateDto Map(EmailTemplate e) =>
        new(e.Id, e.TenantId, e.Key, e.DisplayName, e.Subject, e.HtmlBody, e.TextBody, e.CreatedAtUtc, e.UpdatedAtUtc);
}

public sealed class UpdateEmailTemplateCommandHandler(IEmailTemplateRepository repository)
    : IRequestHandler<UpdateEmailTemplateCommand, ServiceResult<EmailTemplateDto>>
{
    public async Task<ServiceResult<EmailTemplateDto>> Handle(UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
        if (entity is null)
            return ServiceResult<EmailTemplateDto>.Fail(404, "Template não encontrado.");

        var body = request.Body;
        if (body.Subject is not null)
        {
            if (string.IsNullOrWhiteSpace(body.Subject))
                return ServiceResult<EmailTemplateDto>.Fail(400, "Subject não pode ser vazio.");
            entity.Subject = body.Subject.Trim();
        }

        if (body.DisplayName is not null)
            entity.DisplayName = string.IsNullOrWhiteSpace(body.DisplayName) ? null : body.DisplayName.Trim();

        if (body.HtmlBody is not null)
        {
            if (string.IsNullOrWhiteSpace(body.HtmlBody))
                return ServiceResult<EmailTemplateDto>.Fail(400, "HtmlBody não pode ser vazio.");
            entity.HtmlBody = body.HtmlBody;
        }

        if (body.TextBody is not null)
            entity.TextBody = string.IsNullOrWhiteSpace(body.TextBody) ? null : body.TextBody;

        entity.UpdatedAtUtc = DateTime.UtcNow;
        await repository.ReplaceAsync(entity, cancellationToken);
        return ServiceResult<EmailTemplateDto>.Ok(Map(entity));
    }

    private static EmailTemplateDto Map(EmailTemplate e) =>
        new(e.Id, e.TenantId, e.Key, e.DisplayName, e.Subject, e.HtmlBody, e.TextBody, e.CreatedAtUtc, e.UpdatedAtUtc);
}

public sealed class DeleteEmailTemplateCommandHandler(IEmailTemplateRepository repository)
    : IRequestHandler<DeleteEmailTemplateCommand, ServiceResult<object?>>
{
    public async Task<ServiceResult<object?>> Handle(DeleteEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
        if (entity is null)
            return ServiceResult<object?>.Fail(404, "Template não encontrado.");

        await repository.DeleteAsync(request.TenantId, request.Id, cancellationToken);
        return ServiceResult<object?>.Ok(null, 204);
    }
}
