using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
public sealed class AccountsController(
    IAccountRegistrationAppService accountRegistrationAppService,
    IAccountUpdateAppService accountUpdateAppService) : ControllerBase
{
    /// <summary>
    /// Registo público de conta. Exige <c>email</c>, <c>username</c> e <c>password</c> (JSON Schema do tenant).
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
    /// Apenas o próprio utilizador ou um administrador do tenant pode atualizar. O documento resultante é validado contra o JSON Schema default do tenant.
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
        var result = await accountUpdateAppService.UpdateAsync(
            tenantId,
            userId,
            actorUserId,
            roles,
            body.GetRawText(),
            cancellationToken);

        return result.ToApiResponse();
    }
}
