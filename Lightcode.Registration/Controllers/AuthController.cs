using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthenticationAppService authenticationAppService) : ControllerBase
{
    /// <summary>
    /// Emite JWT com <c>sub</c> (id do utilizador) e <c>tenantId</c> após validar username e password.
    /// Envie o tenant no cabeçalho <see cref="TenantHttpHeaders.TenantId"/> (pedido anónimo, sem JWT).
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueToken([FromBody] IssueTokenRequest body, CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request);
        if (tenantId is null)
            return ApiResponse.Error(400, $"Cabeçalho obrigatório: {TenantHttpHeaders.TenantId}.");

        var result = await authenticationAppService.IssueTokenAsync(body, tenantId, cancellationToken);
        return result.ToApiResponse();
    }
}
