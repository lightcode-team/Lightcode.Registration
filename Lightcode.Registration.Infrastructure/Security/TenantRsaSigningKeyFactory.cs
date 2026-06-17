using System.Security.Cryptography;
using System.Text.Json;
using Lightcode.Registration.Application.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace Lightcode.Registration.Infrastructure.Security;

internal static class TenantRsaSigningKeyFactory
{
    public static TenantSigningKeyMaterial Create(int version = 1)
    {
        using var rsa = RSA.Create(2048);
        var privateKeyBase64 = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        var keyId = Guid.NewGuid().ToString("N");

        var publicParameters = rsa.ExportParameters(includePrivateParameters: false);
        var publicKey = new RsaSecurityKey(publicParameters)
        {
            KeyId = keyId
        };

        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        jwk.Kid = keyId;
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;

        return new TenantSigningKeyMaterial(
            privateKeyBase64,
            JsonSerializer.Serialize(jwk),
            keyId,
            version);
    }
}
