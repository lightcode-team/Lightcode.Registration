using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Queries;

public sealed record ListEmailTemplatesQuery(string TenantId) : IRequest<ServiceResult<IReadOnlyList<EmailTemplateDto>>>;
