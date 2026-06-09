using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Emails.Commands;
using Lightcode.Registration.Application.Emails.Queries;
using Lightcode.Registration.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.EmailApi.Controllers;

[ApiController]
[Route("api/email-templates")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class EmailTemplatesController(IMediator mediator) : ControllerBase
{
    private string CurrentTenantId =>
        User.FindFirst("tenantId")?.Value ?? throw new InvalidOperationException("tenantId em falta no token.");

    [Authorize(Policy = EmailApiPolicyNames.TemplateRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListEmailTemplatesQuery(CurrentTenantId), cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = EmailApiPolicyNames.TemplateRead)]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetEmailTemplateByIdQuery(CurrentTenantId, id), cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = EmailApiPolicyNames.TemplateWrite)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmailTemplateRequest body, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateEmailTemplateCommand(CurrentTenantId, body), cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = EmailApiPolicyNames.TemplateWrite)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateEmailTemplateRequest body, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateEmailTemplateCommand(CurrentTenantId, id, body), cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = EmailApiPolicyNames.TemplateWrite)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteEmailTemplateCommand(CurrentTenantId, id), cancellationToken);
        return result.ToApiResponse();
    }
}
