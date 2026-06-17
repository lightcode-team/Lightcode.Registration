namespace Lightcode.Registration.Domain.Entities;

/// <summary>Registro de tenant no banco master (metadados e isolamento por database).</summary>
public class Tenant
{
    public string Id { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string DatabaseName { get; set; } = default!;

    /// <summary>Se preenchido, substitui a connection global (ex.: cluster dedicado ao cliente).</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Chave privada RSA criptografada usada para assinar tokens deste tenant.</summary>
    public string? SigningPrivateKeyEncrypted { get; set; }

    /// <summary>Chave publica RSA em JWK, exposta via JWKS.</summary>
    public string? SigningPublicKeyJwk { get; set; }

    public string? SigningKeyId { get; set; }

    public int SigningKeyVersion { get; set; } = 1;

    public DateTime CreatedAt { get; set; }

    public DateTime? SigningKeyCreatedAt { get; set; }

    public bool Active { get; set; } = true;
}
