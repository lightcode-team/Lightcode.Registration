using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthenticationAppService authenticationAppService) : ControllerBase
{
    /// <summary>Emite JWT com <c>sub</c> e <c>tenantId</c> (fluxo de demonstração; substitua por login real).</summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueToken([FromBody] IssueTokenRequest body, CancellationToken cancellationToken)
    {
        var result = await authenticationAppService.IssueTokenAsync(body, cancellationToken);
        return result.ToApiResponse();
    }
}
