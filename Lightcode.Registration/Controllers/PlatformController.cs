using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/platform")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = PlatformPolicyNames.PlatformAdmin)]
public sealed class PlatformController(IPlatformAdminAppService platformAdminAppService) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants(CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("platformAdminId")?.Value;
        if (string.IsNullOrWhiteSpace(adminId))
            return ApiResponse.Error(401, "Administrador central em falta no token.");

        var result = await platformAdminAppService.ListTenantsAsync(adminId, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost("tenants/{tenantId}/token")]
    public async Task<IActionResult> IssueTenantToken(string tenantId, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("platformAdminId")?.Value;
        if (string.IsNullOrWhiteSpace(adminId))
            return ApiResponse.Error(401, "Administrador central em falta no token.");

        var result = await platformAdminAppService.IssueTenantTokenAsync(adminId, tenantId, cancellationToken);
        return result.ToApiResponse();
    }
}
