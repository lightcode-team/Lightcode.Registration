using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/platform-admins")]
public sealed class PlatformAdminsController(IPlatformAdminAppService platformAdminAppService) : ControllerBase
{
    [HttpPost("invite")]
    [AllowAnonymous]
    public async Task<IActionResult> Invite([FromBody] InvitePlatformAdminRequest body, CancellationToken cancellationToken)
    {
        Request.Headers.TryGetValue("X-Provisioning-Key", out var keyHeader);
        var provisioningKey = keyHeader.FirstOrDefault();

        var result = await platformAdminAppService.InviteAsync(
            new InvitePlatformAdminCommand(body.Email, body.TenantIds, provisioningKey),
            cancellationToken);

        return result.ToApiResponse();
    }

    [HttpPost("activate")]
    [AllowAnonymous]
    public async Task<IActionResult> Activate([FromBody] ActivatePlatformAdminRequest body, CancellationToken cancellationToken)
    {
        var result = await platformAdminAppService.ActivateAsync(body, cancellationToken);
        return result.ToApiResponse();
    }
}
