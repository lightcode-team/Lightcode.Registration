namespace Lightcode.Registration.Application.Abstractions;

public sealed record TenantSigningKeyMaterial(
    string PrivateKeyBase64,
    string PublicKeyJwk,
    string KeyId,
    int Version);

public interface ITenantSigningKeyResolver
{
    Task<TenantSigningKeyMaterial> ResolveSigningKeyAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    string? ResolvePublicKeyJwk(string tenantId);
}
