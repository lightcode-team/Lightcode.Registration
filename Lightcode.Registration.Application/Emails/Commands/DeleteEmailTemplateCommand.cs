using Lightcode.Registration.Application.Common;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Commands;

public sealed record DeleteEmailTemplateCommand(string TenantId, string Id) : IRequest<ServiceResult<object?>>;
