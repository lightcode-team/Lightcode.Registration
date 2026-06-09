using System.Security.Cryptography;
using Lightcode.Registration.Application.Abstractions;

namespace Lightcode.Registration.Infrastructure.Security;

public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public string GenerateRefreshToken() => GenerateUrlSafeToken(48);

    public string GenerateClientSecret() => GenerateUrlSafeToken(32);

    public string GenerateEmailConfirmationCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    public string GenerateEmailConfirmationToken() => GenerateUrlSafeToken(32);

    public string GeneratePasswordResetToken() => GenerateUrlSafeToken(32);

    private static string GenerateUrlSafeToken(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
