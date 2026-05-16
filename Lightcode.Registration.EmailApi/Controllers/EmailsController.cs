using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Emails.Commands;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.EmailApi.Controllers;

[ApiController]
[Route("api/emails")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class EmailsController(IMediator mediator) : ControllerBase
{
    private string CurrentTenantId =>
        User.FindFirst("tenantId")?.Value ?? throw new InvalidOperationException("tenantId em falta no token.");

    /// <summary>Enfileira envio de email (CQRS + RabbitMQ). Use <c>templateId</c> ou <c>templateKey</c>, não ambos.</summary>
    //[Authorize(Policy = "TenantAdmin")]
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest body, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SendEmailCommand(CurrentTenantId, body), cancellationToken);
        return result.ToApiResponse();
    }
}
