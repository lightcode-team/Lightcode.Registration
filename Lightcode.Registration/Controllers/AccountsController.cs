using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Accounts;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
public sealed class AccountsController(
    IAccountRegistrationAppService accountRegistrationAppService,
    IAccountAdminAppService accountAdminAppService,
    IAccountUpdateAppService accountUpdateAppService,
    IAccountEmailConfirmationAppService accountEmailConfirmationAppService) : ControllerBase
{
    /// <summary>
    /// Registo público de conta. Exige <c>schemaId</c>, <c>email</c>, <c>username</c> e <c>password</c> (JSON Schema indicado).
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
    /// Atualização parcial da conta (merge sobre o documento existente). O tenant vem do claim <c>tenantId</c> do JWT.
    /// Apenas o próprio utilizador ou um administrador do tenant pode atualizar. O documento resultante é validado contra o JSON Schema da conta (<c>schemaId</c>).
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

        var result = await accountEmailConfirmationAppService.ConfirmByLinkAsync(
            tenantId.Trim(),
            email,
            token,
            cancellationToken);

        return result.ToApiResponse();
    }
}
