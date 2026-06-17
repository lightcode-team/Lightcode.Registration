namespace Lightcode.Registration.Application.Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Chave mestra usada para criptografar chaves privadas RSA de tenants.</summary>
    public string TenantSigningKeyEncryptionKey { get; set; } = "";
}
