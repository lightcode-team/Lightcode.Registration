using System.Security.Cryptography;
using System.Text;

namespace Lightcode.Registration.Application.Security;

public static class OAuthPkce
{
    public static bool IsValidChallenge(string? challenge, string? method) =>
        string.Equals(method?.Trim(), "S256", StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(challenge)
        && challenge.Length is >= 43 and <= 128
        && challenge.All(IsBase64UrlCharacter);

    public static bool IsValidVerifier(string? verifier) =>
        !string.IsNullOrWhiteSpace(verifier)
        && verifier.Length is >= 43 and <= 128
        && verifier.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~');

    public static string CreateChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    public static string HashOpaqueToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static bool IsBase64UrlCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
