using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/platform-auth")]
public sealed class PlatformAuthController(IPlatformAdminAppService platformAdminAppService) : ControllerBase
{
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueToken([FromBody] PlatformAdminTokenRequest body, CancellationToken cancellationToken)
    {
        var result = await platformAdminAppService.IssueTokenAsync(body, cancellationToken);
        return result.ToApiResponse();
    }
}
