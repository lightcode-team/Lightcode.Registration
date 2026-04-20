using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Commands;

public sealed record UpdateEmailTemplateCommand(string TenantId, string Id, UpdateEmailTemplateRequest Body)
    : IRequest<ServiceResult<EmailTemplateDto>>;
