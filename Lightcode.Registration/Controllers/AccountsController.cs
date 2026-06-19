using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Accounts;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Http;
using Lightcode.Registration.AspNetCore.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
public sealed class AccountsController(
    IAccountRegistrationAppService accountRegistrationAppService,
    IAccountAdminAppService accountAdminAppService,
    IAccountUpdateAppService accountUpdateAppService,
    IAccountCompleteRegistrationAppService accountCompleteRegistrationAppService,
    IAccountEmailConfirmationAppService accountEmailConfirmationAppService,
    IAccountPasswordResetAppService accountPasswordResetAppService,
    IAccountTwoFactorAppService accountTwoFactorAppService,
    HumanAuthRateLimiter rateLimiter) : ControllerBase
{
    /// <summary>
    /// Registo público de conta (salvamento parcial). Exige <c>schemaId</c>, <c>email</c>, <c>username</c> e <c>password</c>.
    /// Campos <c>required</c> do JSON Schema não são obrigatórios nesta etapa; use <c>POST /api/accounts/{userId}/complete-register</c> para concluir.
    /// Preferir o cabeçalho <see cref="TenantHttpHeaders.TenantId"/>; o cabeçalho tem prioridade sobre <paramref name="tenantId"/> na URL.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("~/api/accounts")]
    public Task<IActionResult> Register([FromBody] JsonElement body, CancellationToken cancellationToken) =>
        RegisterCore(routeTenantId: null, body, cancellationToken);

    /// <summary>Rota legada: tenant no path se o cabeçalho <see cref="TenantHttpHeaders.TenantId"/> não for enviado.</summary>
    [AllowAnonymous]
    [HttpPost("~/api/tenants/{tenantId}/accounts")]
    public Task<IActionResult> RegisterWithTenantInPath(string tenantId, [FromBody] JsonElement body, CancellationToken cancellationToken) =>
        RegisterCore(tenantId, body, cancellationToken);

    /// <summary>Lista contas do tenant. Requer role <c>admin</c> ou scope <c>owner</c>.</summary>
    [HttpGet("~/api/accounts")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AccountsPolicyNames.AccountsAdmin)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            return ApiResponse.Error(400, "tenantId em falta no token.");

        var result = await accountAdminAppService.ListAsync(tenantId, cancellationToken);
        return result.ToApiResponse();
    }

    /// <summary>Obtém conta por id (admin). Requer role <c>admin</c> ou scope <c>owner</c>.</summary>
    [HttpGet("~/api/accounts/{userId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AccountsPolicyNames.AccountsAdmin)]
    public async Task<IActionResult> GetById(string userId, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            return ApiResponse.Error(400, "tenantId em falta no token.");

        var result = await accountAdminAppService.GetByIdAsync(tenantId, userId, cancellationToken);
        return result.ToApiResponse();
    }

    /// <summary>
    /// Cria utilizador com roles definidas pelo administrador (role <c>admin</c> ou scope <c>owner</c>).
    /// Exige <c>schemaId</c>; o corpo é validado contra esse JSON Schema (inclui campos adicionais como <c>phone</c>, <c>document</c>).
    /// O tenant vem do claim <c>tenantId</c> do JWT.
    /// </summary>
    [HttpPost("~/api/accounts/admin")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AccountsPolicyNames.AccountsAdmin)]
    public async Task<IActionResult> RegisterByAdmin([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            return ApiResponse.Error(400, "tenantId em falta no token.");

        var result = await accountAdminAppService.RegisterByAdminAsync(tenantId, body.GetRawText(), cancellationToken);
        return result.ToApiResponse();
    }

    /// <summary>
    /// Atualiza as roles de um utilizador. Requer role <c>admin</c> ou scope <c>owner</c>.
    /// O tenant vem do claim <c>tenantId</c> do JWT.
    /// </summary>
    [HttpPut("~/api/accounts/{userId}/roles")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AccountsPolicyNames.AccountsAdmin)]
    public async Task<IActionResult> UpdateRoles(string userId, [FromBody] UpdateAccountRolesRequest body, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            return ApiResponse.Error(400, "tenantId em falta no token.");

        var result = await accountAdminAppService.UpdateRolesAsync(tenantId, userId, body, cancellationToken);
        return result.ToApiResponse();
    }

    private async Task<IActionResult> RegisterCore(string? routeTenantId, JsonElement body, CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request)
            ?? (string.IsNullOrWhiteSpace(routeTenantId) ? null : routeTenantId.Trim());
        if (tenantId is null)
        {
            return ApiResponse.Error(
                400,
                $"Tenant em falta: envie o cabeçalho {TenantHttpHeaders.TenantId} ou use POST /api/tenants/{{tenantId}}/accounts.");
        }

        var json = body.GetRawText();
        var result = await accountRegistrationAppService.RegisterAsync(tenantId, json, cancellationToken);
        return result.ToApiResponse();
    }

    /// <summary>
    /// Conclui o registo: valida todos os campos <c>required</c> do JSON Schema e ativa a conta (ou envia confirmação 2FA).
    /// O tenant vem do claim <c>tenantId</c> do JWT. Apenas o próprio utilizador ou um administrador pode concluir.
    /// </summary>
    [HttpPost("~/api/accounts/{userId}/complete-register")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "HasTenant")]
    public async Task<IActionResult> CompleteRegister(
        string userId,
        [FromBody] CompleteRegisterRequest? body,
        CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            return ApiResponse.Error(400, "tenantId em falta no token.");

        var actorUserId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(actorUserId))
            return ApiResponse.Error(401, "Identificador de utilizador em falta no token.");

        var roles = User.FindAll("role").Select(c => c.Value);
        var scopes = User.FindAll("scope").Select(c => c.Value);
        var result = await accountCompleteRegistrationAppService.CompleteRegisterAsync(
            tenantId,
            userId,
            actorUserId,
            roles,
            scopes,
            body,
            cancellationToken);

        return result.ToApiResponse();
    }

    /// <summary>
    /// Atualização parcial da conta (merge sobre o documento existente). O tenant vem do claim <c>tenantId</c> do JWT.
    /// Apenas o próprio utilizador ou um administrador do tenant pode atualizar. Campos <c>required</c> do schema não são exigidos nesta etapa.
    /// </summary>
    [HttpPut("~/api/accounts/{userId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "HasTenant")]
    public async Task<IActionResult> Update(string userId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            return ApiResponse.Error(400, "tenantId em falta no token.");

        var actorUserId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(actorUserId))
            return ApiResponse.Error(401, "Identificador de utilizador em falta no token.");

        var roles = User.FindAll("role").Select(c => c.Value);
        var scopes = User.FindAll("scope").Select(c => c.Value);
        var result = await accountUpdateAppService.UpdateAsync(
            tenantId,
            userId,
            actorUserId,
            roles,
            scopes,
            body.GetRawText(),
            cancellationToken);

        return result.ToApiResponse();
    }

    /// <summary>Confirmação de email em modo <c>2FA.Type = Code</c>. Exige cabeçalho <see cref="TenantHttpHeaders.TenantId"/>.</summary>
    [AllowAnonymous]
    [HttpPost("~/api/accounts/confirm-email-code/{code}")]
    public async Task<IActionResult> ConfirmEmailCode(
        string code,
        [FromBody] ConfirmEmailCodeRequest body,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request);
        if (tenantId is null)
            return ApiResponse.Error(400, $"Tenant em falta: envie o cabeçalho {TenantHttpHeaders.TenantId}.");

        if (rateLimiter.LimitAccountRecovery(HttpContext, tenantId, body.Email) is { } limited)
            return limited;

        var result = await accountEmailConfirmationAppService.ConfirmByCodeAsync(
            tenantId,
            body.Email,
            code,
            cancellationToken);

        return result.ToApiResponse();
    }

    /// <summary>Confirmação de email em modo <c>2FA.Type = Link</c>. Query: <c>tenantId</c> e <c>email</c>.</summary>
    [AllowAnonymous]
    [HttpGet("~/api/accounts/confirm-email/{token}")]
    public async Task<IActionResult> ConfirmEmailLink(
        string token,
        [FromQuery] string tenantId,
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ApiResponse.Error(400, "tenantId é obrigatório na query.");

        if (rateLimiter.LimitAccountRecovery(HttpContext, tenantId, email) is { } limited)
            return limited;

        var result = await accountEmailConfirmationAppService.ConfirmByLinkAsync(
            tenantId.Trim(),
            email,
            token,
            cancellationToken);

        return result.ToApiResponse();
    }

    /// <summary>Solicita redefinição de senha por email ou username. Envia link por email se a conta existir. Exige cabeçalho <see cref="TenantHttpHeaders.TenantId"/>.</summary>
    [AllowAnonymous]
    [HttpPost("~/api/accounts/forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest body,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request);
        if (tenantId is null)
            return ApiResponse.Error(400, $"Tenant em falta: envie o cabeçalho {TenantHttpHeaders.TenantId}.");

        if (rateLimiter.LimitAccountRecovery(HttpContext, tenantId, body.Email ?? body.Username) is { } limited)
            return limited;

        var result = await accountPasswordResetAppService.ForgotPasswordAsync(
            tenantId,
            body.Email,
            body.Username,
            cancellationToken);

        return result.ToApiResponse();
    }

    [HttpPost("~/api/accounts/me/2fa/email/enable/begin")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "HasTenant")]
    public async Task<IActionResult> BeginEnableEmailTwoFactor(CancellationToken cancellationToken)
    {
        var context = ResolveCurrentTenantUser();
        if (context.Result is not null)
            return context.Result;

        if (rateLimiter.LimitTwoFactorManagement(HttpContext, context.TenantId, context.UserId) is { } limited)
            return limited;

        var result = await accountTwoFactorAppService.BeginEnableEmailAsync(
            context.TenantId!,
            context.UserId!,
            cancellationToken);

        return result.ToApiResponse();
    }

    [HttpPost("~/api/accounts/me/2fa/email/enable/confirm")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "HasTenant")]
    public async Task<IActionResult> ConfirmEnableEmailTwoFactor(
        [FromBody] ConfirmTwoFactorRequest body,
        CancellationToken cancellationToken)
    {
        var context = ResolveCurrentTenantUser();
        if (context.Result is not null)
            return context.Result;

        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "two_factor_management_confirm",
                context.TenantId,
                body.ChallengeId) is { } limited)
            return limited;

        var result = await accountTwoFactorAppService.ConfirmEnableEmailAsync(
            context.TenantId!,
            context.UserId!,
            body,
            cancellationToken);

        return result.ToApiResponse();
    }

    [HttpPost("~/api/accounts/me/2fa/disable/begin")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "HasTenant")]
    public async Task<IActionResult> BeginDisableTwoFactor(CancellationToken cancellationToken)
    {
        var context = ResolveCurrentTenantUser();
        if (context.Result is not null)
            return context.Result;

        if (rateLimiter.LimitTwoFactorManagement(HttpContext, context.TenantId, context.UserId) is { } limited)
            return limited;

        var result = await accountTwoFactorAppService.BeginDisableAsync(
            context.TenantId!,
            context.UserId!,
            cancellationToken);

        return result.ToApiResponse();
    }

    [HttpPost("~/api/accounts/me/2fa/disable/confirm")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "HasTenant")]
    public async Task<IActionResult> ConfirmDisableTwoFactor(
        [FromBody] ConfirmTwoFactorRequest body,
        CancellationToken cancellationToken)
    {
        var context = ResolveCurrentTenantUser();
        if (context.Result is not null)
            return context.Result;

        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "two_factor_management_confirm",
                context.TenantId,
                body.ChallengeId) is { } limited)
            return limited;

        var result = await accountTwoFactorAppService.ConfirmDisableAsync(
            context.TenantId!,
            context.UserId!,
            body,
            cancellationToken);

        return result.ToApiResponse();
    }

    private (string? TenantId, string? UserId, IActionResult? Result) ResolveCurrentTenantUser()
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
            return (null, null, ApiResponse.Error(400, "tenantId em falta no token."));

        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return (null, null, ApiResponse.Error(401, "Identificador de utilizador em falta no token."));

        return (tenantId, userId, null);
    }
}
