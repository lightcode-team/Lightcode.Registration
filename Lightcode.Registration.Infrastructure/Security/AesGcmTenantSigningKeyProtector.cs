using System.Security.Cryptography;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Infrastructure.Security;

public sealed class AesGcmTenantSigningKeyProtector(IOptions<SecurityOptions> options) : ITenantSigningKeyProtector
{
    private const string Prefix = "v1";
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public string Protect(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("Material de assinatura não pode ser vazio.", nameof(signingKey));

        var plaintext = Encoding.UTF8.GetBytes(signingKey);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(GetMasterKey(), TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSizeBytes + TagSizeBytes, ciphertext.Length);

        return $"{Prefix}:{Convert.ToBase64String(payload)}";
    }

    public string Unprotect(string encryptedSigningKey)
    {
        if (string.IsNullOrWhiteSpace(encryptedSigningKey))
            throw new ArgumentException("Material de assinatura criptografado não pode ser vazio.", nameof(encryptedSigningKey));

        var parts = encryptedSigningKey.Split(':', 2);
        if (parts.Length != 2 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Formato do material de assinatura criptografado não suportado.");

        var payload = Convert.FromBase64String(parts[1]);
        if (payload.Length <= NonceSizeBytes + TagSizeBytes)
            throw new InvalidOperationException("Payload do material de assinatura criptografado é inválido.");

        var nonce = payload[..NonceSizeBytes];
        var tag = payload[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var ciphertext = payload[(NonceSizeBytes + TagSizeBytes)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(GetMasterKey(), TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] GetMasterKey()
    {
        var configured = options.Value.TenantSigningKeyEncryptionKey;
        if (string.IsNullOrWhiteSpace(configured) || configured.Trim().Length < 32)
            throw new InvalidOperationException("Configure Security:TenantSigningKeyEncryptionKey com pelo menos 32 caracteres.");

        return SHA256.HashData(Encoding.UTF8.GetBytes(configured.Trim()));
    }
}
