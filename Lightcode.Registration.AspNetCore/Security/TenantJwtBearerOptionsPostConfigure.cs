using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Lightcode.Registration.AspNetCore.Security;

public sealed class TenantJwtBearerOptionsPostConfigure(
    IServiceScopeFactory scopeFactory,
    IOptions<JwtOptions> jwtOptions) : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        options.TokenValidationParameters.IssuerSigningKeyResolver = ResolveSigningKeys;
    }

    private IEnumerable<SecurityKey> ResolveSigningKeys(
        string token,
        SecurityToken? securityToken,
        string? kid,
        TokenValidationParameters validationParameters)
    {
        return ResolveSigningKeys(token);
    }

    private IEnumerable<SecurityKey> ResolveSigningKeys(string token)
    {
        var tenantId = TryReadTenantId(token);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return [new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey))];
        }

        using var scope = scopeFactory.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantSigningKeyResolver>();
        var publicJwk = resolver.ResolvePublicKeyJwk(tenantId);
        return string.IsNullOrWhiteSpace(publicJwk)
            ? []
            : [new JsonWebKey(publicJwk)];
    }

    private static string? TryReadTenantId(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, "tenantId", StringComparison.Ordinal))?.Value;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
