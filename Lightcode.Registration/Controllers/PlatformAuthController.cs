using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.AspNetCore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/platform-auth")]
public sealed class PlatformAuthController(
    IPlatformAdminAppService platformAdminAppService,
    HumanAuthRateLimiter rateLimiter) : ControllerBase
{
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueToken([FromBody] PlatformAdminTokenRequest body, CancellationToken cancellationToken)
    {
        if (rateLimiter.LimitPlatformPasswordGrant(HttpContext, body.Email) is { } limited)
            return limited;

        var result = await platformAdminAppService.IssueTokenAsync(body, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost("confirm-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] ConfirmTwoFactorRequest body, CancellationToken cancellationToken)
    {
        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "platform_confirm_2fa",
                tenantId: null,
                body.ChallengeId) is { } limited)
            return limited;

        var result = await platformAdminAppService.ConfirmTwoFactorAsync(body, cancellationToken);
        return result.ToApiResponse();
    }
}
