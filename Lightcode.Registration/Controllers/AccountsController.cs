using System.Text.Json;
using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/tenants/{tenantId}/accounts")]
[AllowAnonymous]
public sealed class AccountsController(IAccountRegistrationAppService accountRegistrationAppService) : ControllerBase
{
    /// <summary>Registo público de conta. O corpo é validado contra o JSON Schema default do tenant.</summary>
    [HttpPost]
    public async Task<IActionResult> Register(string tenantId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var json = body.GetRawText();
        var result = await accountRegistrationAppService.RegisterAsync(tenantId, json, cancellationToken);
        return result.ToApiResponse();
    }
}
