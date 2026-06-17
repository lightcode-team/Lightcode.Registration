using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Lightcode.Registration.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class TenantDiscoveryController(
    ITenantSigningKeyResolver signingKeyResolver,
    IOptions<JwtOptions> jwtOptions,
    IOptions<RegistrationOptions> registrationOptions) : ControllerBase
{
    [HttpGet("/tenants/{tenantId}/.well-known/jwks.json")]
    public IActionResult GetJwks(string tenantId)
    {
        var publicJwk = signingKeyResolver.ResolvePublicKeyJwk(tenantId);
        if (string.IsNullOrWhiteSpace(publicJwk))
            return NotFound();

        return Content($$"""{"keys":[{{publicJwk}}]}""", "application/json");
    }

    [HttpGet("/tenants/{tenantId}/.well-known/openid-configuration")]
    public IActionResult GetOpenIdConfiguration(string tenantId)
    {
        var publicJwk = signingKeyResolver.ResolvePublicKeyJwk(tenantId);
        if (string.IsNullOrWhiteSpace(publicJwk))
            return NotFound();

        var issuer = TenantTokenIssuer.Build(registrationOptions.Value, jwtOptions.Value, tenantId);
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = issuer,
            JwksUri = $"{issuer}/.well-known/jwks.json"
        };

        return Ok(new
        {
            issuer = configuration.Issuer,
            jwks_uri = configuration.JwksUri,
            id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.RsaSha256 }
        });
    }
}
