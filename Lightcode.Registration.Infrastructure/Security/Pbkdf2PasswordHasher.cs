using System.Security.Cryptography;
using System.Text;
using Lightcode.Registration.Application.Abstractions;

namespace Lightcode.Registration.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string plainTextPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainTextPassword),
            salt,
            Iterations,
            Algorithm,
            KeySize);

        return $"pbkdf2_sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string plainTextPassword, string storedHash)
    {
        if (string.IsNullOrEmpty(plainTextPassword) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split('$', 4, StringSplitOptions.None);
        if (parts.Length != 4 || parts[0] != "pbkdf2_sha256")
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations < 1)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainTextPassword),
            salt,
            iterations,
            Algorithm,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
