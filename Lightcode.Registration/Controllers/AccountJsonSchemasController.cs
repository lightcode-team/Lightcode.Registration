using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.JsonSchema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/account-json-schemas")]
[Authorize(Policy = "HasTenant")]
public sealed class AccountJsonSchemasController(IAccountJsonSchemaAppService accountJsonSchemaAppService) : ControllerBase
{
    private string CurrentTenantId =>
        User.FindFirst("tenantId")?.Value ?? throw new InvalidOperationException("tenantId em falta no token.");

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.ListAsync(CurrentTenantId, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.GetByIdAsync(CurrentTenantId, id, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountJsonSchemaRequest body, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.CreateAsync(CurrentTenantId, body, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateAccountJsonSchemaRequest body, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.UpdateAsync(CurrentTenantId, id, body, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await accountJsonSchemaAppService.DeleteAsync(CurrentTenantId, id, cancellationToken);
        return result.ToApiResponse();
    }
}
