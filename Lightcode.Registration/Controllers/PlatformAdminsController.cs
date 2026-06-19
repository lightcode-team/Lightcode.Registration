using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.AspNetCore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/platform-admins")]
public sealed class PlatformAdminsController(
    IPlatformAdminAppService platformAdminAppService,
    HumanAuthRateLimiter rateLimiter) : ControllerBase
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

    [HttpPost("me/2fa/email/enable/begin")]
    [Authorize(Policy = PlatformPolicyNames.PlatformAdmin)]
    public async Task<IActionResult> BeginEnableEmailTwoFactor(CancellationToken cancellationToken)
    {
        var adminId = GetPlatformAdminId();
        if (rateLimiter.LimitTwoFactorManagement(HttpContext, tenantId: null, adminId) is { } limited)
            return limited;

        var result = await platformAdminAppService.BeginEnableEmailTwoFactorAsync(adminId, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost("me/2fa/email/enable/confirm")]
    [Authorize(Policy = PlatformPolicyNames.PlatformAdmin)]
    public async Task<IActionResult> ConfirmEnableEmailTwoFactor(
        [FromBody] ConfirmTwoFactorRequest body,
        CancellationToken cancellationToken)
    {
        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "platform_two_factor_management_confirm",
                tenantId: null,
                body.ChallengeId) is { } limited)
            return limited;

        var result = await platformAdminAppService.ConfirmEnableEmailTwoFactorAsync(
            GetPlatformAdminId(),
            body,
            cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost("me/2fa/disable/begin")]
    [Authorize(Policy = PlatformPolicyNames.PlatformAdmin)]
    public async Task<IActionResult> BeginDisableTwoFactor(CancellationToken cancellationToken)
    {
        var adminId = GetPlatformAdminId();
        if (rateLimiter.LimitTwoFactorManagement(HttpContext, tenantId: null, adminId) is { } limited)
            return limited;

        var result = await platformAdminAppService.BeginDisableTwoFactorAsync(adminId, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost("me/2fa/disable/confirm")]
    [Authorize(Policy = PlatformPolicyNames.PlatformAdmin)]
    public async Task<IActionResult> ConfirmDisableTwoFactor(
        [FromBody] ConfirmTwoFactorRequest body,
        CancellationToken cancellationToken)
    {
        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "platform_two_factor_management_confirm",
                tenantId: null,
                body.ChallengeId) is { } limited)
            return limited;

        var result = await platformAdminAppService.ConfirmDisableTwoFactorAsync(
            GetPlatformAdminId(),
            body,
            cancellationToken);
        return result.ToApiResponse();
    }

    private string GetPlatformAdminId() =>
        User.FindFirst("platformAdminId")?.Value ?? string.Empty;
}
