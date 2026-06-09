using System.Security.Cryptography;
using System.Text;

namespace Lightcode.Registration.Infrastructure.Security;

internal static class TokenHashing
{
    public static string HashRefreshToken(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
