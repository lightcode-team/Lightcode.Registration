using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.JsonSchema;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/account-json-schemas")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class AccountJsonSchemasController(IAccountJsonSchemaAppService accountJsonSchemaAppService) : ControllerBase
{
    /// <summary>Tenant do contexto, obtido do claim <c>tenantId</c> do JWT.</summary>
    private string CurrentTenantId =>
        User.FindFirst("tenantId")?.Value ?? throw new InvalidOperationException("tenantId em falta no token.");

    [Authorize(Policy = "HasTenant")]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.ListAsync(CurrentTenantId, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = "HasTenant")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.GetByIdAsync(CurrentTenantId, id, cancellationToken);
        return result.ToApiResponse();
    }

    /// <summary>Apenas role <c>admin</c> pode criar schemas.</summary>
    [Authorize(Policy = "TenantAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountJsonSchemaRequest body, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.CreateAsync(CurrentTenantId, body, cancellationToken);
        return result.ToApiResponse();
    }

    /// <summary>Apenas role <c>admin</c> pode atualizar schemas.</summary>
    [Authorize(Policy = "TenantAdmin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateAccountJsonSchemaRequest body, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.UpdateAsync(CurrentTenantId, id, body, cancellationToken);
        return result.ToApiResponse();
    }

    /// <summary>Apenas role <c>admin</c> pode apagar schemas.</summary>
    [Authorize(Policy = "TenantAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.DeleteAsync(CurrentTenantId, id, cancellationToken);
        return result.ToApiResponse();
    }
}
