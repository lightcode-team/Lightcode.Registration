using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.AspNetCore.Security;

public interface IJwtTenantTokenValidator
{
    Task ValidateAsync(TokenValidatedContext context);
}

public sealed class JwtTenantTokenValidator(
    IOAuthClientRepository oauthClientRepository,
    ITenantLookup tenantLookup,
    IOptions<JwtOptions> jwtOptions,
    IOptions<RegistrationOptions> registrationOptions) : IJwtTenantTokenValidator
{
    public async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            context.Fail("Utilizador não autenticado.");
            return;
        }

        var tenantId = principal.FindFirst("tenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            ValidatePlatformAdminToken(context);
            return;
        }

        var tenant = await tenantLookup.FindActiveByIdAsync(
            tenantId,
            context.HttpContext.RequestAborted);
        if (tenant is null)
        {
            context.Fail("Tenant do token não encontrado ou inativo.");
            return;
        }

        var clientId = principal.FindFirst("client_id")?.Value;
        TokenIssuanceProfile expected;

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var client = await oauthClientRepository.FindByClientIdAsync(
                tenantId,
                clientId,
                context.HttpContext.RequestAborted);

            if (client is null)
            {
                context.Fail("Cliente OAuth do token não encontrado.");
                return;
            }

            expected = TokenIssuanceProfile.FromOAuthClient(client);
        }
        else
        {
            expected = TokenIssuanceProfile.ForPasswordGrant(
                jwtOptions.Value,
                registrationOptions.Value,
                tenantId,
                []);
        }

        var tokenIssuer = JwtClaimReader.GetIssuer(principal);
        if (string.IsNullOrWhiteSpace(tokenIssuer)
            || !string.Equals(tokenIssuer, expected.Issuer, StringComparison.Ordinal))
        {
            context.Fail("Issuer do token inválido.");
            return;
        }

        var tokenAudiences = JwtClaimReader.GetAudiences(principal);
        if (string.IsNullOrWhiteSpace(expected.Audience)
            || !tokenAudiences.Contains(expected.Audience, StringComparer.Ordinal))
        {
            context.Fail("Audience do token inválida.");
            return;
        }
    }

    private void ValidatePlatformAdminToken(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var tokenUse = principal?.FindFirst("token_use")?.Value;
        var adminId = principal?.FindFirst("platformAdminId")?.Value;

        if (!string.Equals(tokenUse, "platform_admin", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(adminId))
        {
            context.Fail("Token sem claim tenantId.");
            return;
        }

        var jwt = jwtOptions.Value;
        var tokenIssuer = JwtClaimReader.GetIssuer(principal!);
        if (string.IsNullOrWhiteSpace(tokenIssuer)
            || !string.Equals(tokenIssuer, jwt.Issuer, StringComparison.Ordinal))
        {
            context.Fail("Issuer do token invÃ¡lido.");
            return;
        }

        var tokenAudiences = JwtClaimReader.GetAudiences(principal!);
        if (string.IsNullOrWhiteSpace(jwt.Audience)
            || !tokenAudiences.Contains(jwt.Audience, StringComparer.Ordinal))
        {
            context.Fail("Audience do token invÃ¡lida.");
        }
    }
}
