using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.OAuthClients;
using Lightcode.Registration.Application.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/oauth-clients")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class OAuthClientsController(IOAuthClientAppService oauthClientAppService) : ControllerBase
{
    private string CurrentTenantId =>
        User.FindFirst("tenantId")?.Value ?? throw new InvalidOperationException("tenantId em falta no token.");

    private string CurrentClientId =>
        User.FindFirst("client_id")?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("client_id em falta no token.");

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsRead)]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.ListAsync(CurrentTenantId, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsRead)]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.GetByClientIdAsync(CurrentTenantId, CurrentClientId, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsRead)]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.GetByIdAsync(CurrentTenantId, id, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsWrite)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOAuthClientRequest body, CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.CreateAsync(CurrentTenantId, body, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsWrite)]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateOAuthClientRequest body, CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.UpdateByClientIdAsync(CurrentTenantId, CurrentClientId, body, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsWrite)]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateById(string id, [FromBody] UpdateOAuthClientRequest body, CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.UpdateByIdAsync(CurrentTenantId, id, body, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsWrite)]
    [HttpDelete("me")]
    public async Task<IActionResult> DeactivateCurrent(CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.DeactivateByClientIdAsync(CurrentTenantId, CurrentClientId, cancellationToken);
        return result.ToApiResponse();
    }

    [Authorize(Policy = OAuthClientsPolicyNames.ClientsWrite)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateById(string id, CancellationToken cancellationToken)
    {
        var result = await oauthClientAppService.DeactivateByIdAsync(CurrentTenantId, id, cancellationToken);
        return result.ToApiResponse();
    }
}
