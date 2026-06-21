using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using FluentAssertions;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class JwtAccessTokenIssuerTests
{
    [Fact]
    public void CreateAccessToken_can_issue_multiple_tokens_with_the_same_tenant_signing_key()
    {
        var issuer = CreateIssuer();
        var signingKey = CreateTenantSigningKey();
        var profile = new TokenIssuanceProfile
        {
            Issuer = "issuer",
            Audience = "audience",
            AccessTokenExpirationMinutes = 60,
            Roles = [UserRoles.User],
            Scopes = ["accounts:read"],
            UserId = "user-1",
            Email = "user@example.com",
            Username = "user@example.com"
        };

        var first = issuer.CreateAccessToken("user-1", "tenant-1", profile, signingKey);
        var second = issuer.CreateAccessToken("user-1", "tenant-1", profile, signingKey);

        first.AccessToken.Should().NotBeNullOrWhiteSpace();
        second.AccessToken.Should().NotBeNullOrWhiteSpace();
        new JwtSecurityTokenHandler().ReadJwtToken(second.AccessToken).Header.Kid.Should().Be(signingKey.KeyId);
    }

    [Fact]
    public void CreatePlatformAdminAccessToken_can_issue_multiple_tokens()
    {
        var issuer = CreateIssuer();

        var first = issuer.CreatePlatformAdminAccessToken("admin-1", "admin@example.com");
        var second = issuer.CreatePlatformAdminAccessToken("admin-1", "admin@example.com");

        first.AccessToken.Should().NotBeNullOrWhiteSpace();
        second.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    private static JwtAccessTokenIssuer CreateIssuer() =>
        new(Options.Create(new JwtOptions
        {
            SigningKey = "12345678901234567890123456789012",
            Issuer = "platform-issuer",
            Audience = "platform-audience",
            ExpirationMinutes = 60
        }));

    private static TenantSigningKeyMaterial CreateTenantSigningKey()
    {
        using var rsa = RSA.Create(2048);
        return new TenantSigningKeyMaterial(
            Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
            "{}",
            Guid.NewGuid().ToString("N"),
            1);
    }
}
