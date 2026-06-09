using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TenantsController(ITenantOnboardingAppService onboardingAppService) : ControllerBase
{
    /// <summary>Cria um novo tenant (database dedicado + registro no master).</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest body, CancellationToken cancellationToken)
    {
        Request.Headers.TryGetValue("X-Provisioning-Key", out var keyHeader);
        var provisioningKey = keyHeader.FirstOrDefault();

        var command = new CreateTenantCommand(body.Name, body.AdminEmail, provisioningKey);
        var result = await onboardingAppService.CreateTenantAsync(command, cancellationToken);
        return result.ToApiResponse();
    }
}

public sealed record CreateTenantRequest(string? Name, string? AdminEmail);
