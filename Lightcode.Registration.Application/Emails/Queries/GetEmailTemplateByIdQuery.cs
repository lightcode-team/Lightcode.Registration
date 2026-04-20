using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Queries;

public sealed record GetEmailTemplateByIdQuery(string TenantId, string Id) : IRequest<ServiceResult<EmailTemplateDto>>;
