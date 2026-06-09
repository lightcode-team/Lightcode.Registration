using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Lightcode.Registration.Infrastructure.Security;

public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> jwtOptions) : IAccessTokenIssuer
{
    public IssueTokenResponse CreateAccessToken(string subjectId, string tenantId, TokenIssuanceProfile profile)
    {
        var jwt = jwtOptions.Value;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId),
            new("tenantId", tenantId)
        };

        if (!string.IsNullOrWhiteSpace(profile.ClientId))
            claims.Add(new Claim("client_id", profile.ClientId));

        foreach (var role in profile.Roles.Where(r => !string.IsNullOrWhiteSpace(r)))
            claims.Add(new Claim("role", role.Trim().ToLowerInvariant()));

        foreach (var scope in profile.Scopes.Where(s => !string.IsNullOrWhiteSpace(s)))
            claims.Add(new Claim("scope", scope.Trim()));

        var expiresMinutes = profile.AccessTokenExpirationMinutes > 0
            ? profile.AccessTokenExpirationMinutes
            : jwt.ExpirationMinutes;

        var token = new JwtSecurityToken(
            issuer: profile.Issuer,
            audience: profile.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds);

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(token);
        return new IssueTokenResponse(accessToken, "Bearer", expiresMinutes * 60);
    }
}
