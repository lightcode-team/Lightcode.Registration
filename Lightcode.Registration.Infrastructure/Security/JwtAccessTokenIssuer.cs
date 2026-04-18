using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Lightcode.Registration.Infrastructure.Security;

public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> jwtOptions) : IAccessTokenIssuer
{
    public IssueTokenResponse CreateToken(string userId, string tenantId)
    {
        var jwt = jwtOptions.Value;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("tenantId", tenantId)
        };

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
