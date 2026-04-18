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
    public IssueTokenResponse CreateToken(string userId, string tenantId, IReadOnlyList<string> roles)
    {
        var jwt = jwtOptions.Value;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var normalized = UserRoles.NormalizeMany(roles);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("tenantId", tenantId)
        };

        foreach (var r in normalized)
            claims.Add(new Claim("role", r));

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.ExpirationMinutes),
            signingCredentials: creds);

        var handler = new JwtSecurityTokenHandler();
        var accessToken = handler.WriteToken(token);
        return new IssueTokenResponse(accessToken, "Bearer", jwt.ExpirationMinutes * 60);
    }
}
