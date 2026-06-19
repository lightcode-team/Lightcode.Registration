using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Http;
using Lightcode.Registration.AspNetCore.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lightcode.Registration.Models;

namespace Lightcode.Registration.Controllers;

public sealed class AuthController(
    IAuthenticationAppService authenticationAppService,
    IFrontConfigAppService frontConfigAppService,
    HumanAuthRateLimiter rateLimiter) : Controller
{
    [HttpGet("/auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var resolvedTenantId = ResolveTenantId(tenantId);
        return View("~/Views/Auth/Login.cshtml", new LoginViewModel
        {
            TenantId = resolvedTenantId,
            FrontConfig = await frontConfigAppService.ResolveAsync(resolvedTenantId, cancellationToken)
        });
    }

    [HttpPost("/auth/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var resolvedTenantId = ResolveTenantId(model.TenantId);
        model.TenantId = resolvedTenantId;
        model.FrontConfig = await frontConfigAppService.ResolveAsync(resolvedTenantId, cancellationToken);

        if (!ModelState.IsValid)
            return View("~/Views/Auth/Login.cshtml", model);

        model.Password = string.Empty;
        model.ErrorMessage = model.FrontConfig.Messages.AuthenticationNotIntegrated;
        return View("~/Views/Auth/Login.cshtml", model);
    }

    /// <summary>
    /// Emite JWT e refresh token. Suporta <c>grant_type</c>: <c>password</c>, <c>refresh_token</c>, <c>client_credentials</c>.
    /// Envie o tenant no cabeçalho <see cref="TenantHttpHeaders.TenantId"/> (pedido anónimo, sem JWT).
    /// </summary>
    [HttpPost("/api/auth/token")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueToken([FromBody] TokenRequest body, CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request);
        if (tenantId is null)
            return ApiResponse.Error(400, $"Cabeçalho obrigatório: {TenantHttpHeaders.TenantId}.");

        if (IsPasswordGrant(body)
            && rateLimiter.LimitPasswordGrant(HttpContext, tenantId, body.Username) is { } limited)
            return limited;

        var result = await authenticationAppService.IssueTokenAsync(body, tenantId, cancellationToken);
        return result.ToApiResponse();
    }

    private string? ResolveTenantId(string? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
            return tenantId.Trim();

        return TenantHttpHeaders.TryGetTenantId(Request);
    }

    [HttpPost("confirm-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] ConfirmTwoFactorRequest body, CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request);
        if (tenantId is null)
            return ApiResponse.Error(400, $"Cabeçalho obrigatório: {TenantHttpHeaders.TenantId}.");

        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "auth_confirm_2fa",
                tenantId,
                body.ChallengeId) is { } limited)
            return limited;

        var result = await authenticationAppService.ConfirmTwoFactorAsync(body, tenantId, cancellationToken);
        return result.ToApiResponse();
    }

    private static bool IsPasswordGrant(TokenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.GrantType))
            return string.Equals(request.GrantType.Trim(), TokenGrantTypes.Password, StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrWhiteSpace(request.Username) || !string.IsNullOrWhiteSpace(request.Password);
    }
}
