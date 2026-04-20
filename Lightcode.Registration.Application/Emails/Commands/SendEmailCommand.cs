using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using MediatR;

namespace Lightcode.Registration.Application.Emails.Commands;

public sealed record SendEmailCommand(string TenantId, SendEmailRequest Body) : IRequest<ServiceResult<SendEmailQueuedDto>>;
