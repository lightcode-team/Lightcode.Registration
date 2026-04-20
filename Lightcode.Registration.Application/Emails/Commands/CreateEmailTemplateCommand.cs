using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Commands;

public sealed record CreateEmailTemplateCommand(string TenantId, CreateEmailTemplateRequest Body)
    : IRequest<ServiceResult<EmailTemplateDto>>;
