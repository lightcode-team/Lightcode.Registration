using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Security;

public sealed class MongoTenantSigningKeyResolver : ITenantSigningKeyResolver
{
    private readonly IMongoCollection<Tenant> _tenants;
    private readonly ITenantSigningKeyProtector _protector;

    public MongoTenantSigningKeyResolver(
        IMongoClient client,
        IOptions<MongoOptions> mongoOptions,
        ITenantSigningKeyProtector protector)
    {
        _tenants = client
            .GetDatabase(mongoOptions.Value.MasterDatabaseName)
            .GetCollection<Tenant>("Tenants");
        _protector = protector;
    }

    public async Task<TenantSigningKeyMaterial> ResolveSigningKeyAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenants
            .Find(x => x.Id == tenantId && x.Active)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
            throw new InvalidOperationException($"Tenant '{tenantId}' não encontrado ou inativo.");

        tenant = await EnsureSigningKeyAsync(tenant, cancellationToken);
        return ToMaterial(tenant);
    }

    public string? ResolvePublicKeyJwk(string tenantId)
    {
        var tenant = _tenants
            .Find(x => x.Id == tenantId && x.Active)
            .FirstOrDefault();

        if (tenant is null)
            return null;

        tenant = EnsureSigningKey(tenant);
        return tenant.SigningPublicKeyJwk;
    }

    private async Task<Tenant> EnsureSigningKeyAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        if (HasSigningKey(tenant))
            return tenant;

        var material = TenantRsaSigningKeyFactory.Create();
        var update = Builders<Tenant>.Update
            .Set(x => x.SigningPrivateKeyEncrypted, _protector.Protect(material.PrivateKeyBase64))
            .Set(x => x.SigningPublicKeyJwk, material.PublicKeyJwk)
            .Set(x => x.SigningKeyId, material.KeyId)
            .Set(x => x.SigningKeyVersion, material.Version)
            .Set(x => x.SigningKeyCreatedAt, DateTime.UtcNow);

        return await _tenants.FindOneAndUpdateAsync(
            x => x.Id == tenant.Id && x.Active,
            update,
            new FindOneAndUpdateOptions<Tenant> { ReturnDocument = ReturnDocument.After },
            cancellationToken)
            ?? throw new InvalidOperationException($"Tenant '{tenant.Id}' não encontrado ou inativo durante migração de chave RSA.");
    }

    private Tenant EnsureSigningKey(Tenant tenant)
    {
        if (HasSigningKey(tenant))
            return tenant;

        var material = TenantRsaSigningKeyFactory.Create();
        var update = Builders<Tenant>.Update
            .Set(x => x.SigningPrivateKeyEncrypted, _protector.Protect(material.PrivateKeyBase64))
            .Set(x => x.SigningPublicKeyJwk, material.PublicKeyJwk)
            .Set(x => x.SigningKeyId, material.KeyId)
            .Set(x => x.SigningKeyVersion, material.Version)
            .Set(x => x.SigningKeyCreatedAt, DateTime.UtcNow);

        return _tenants.FindOneAndUpdate(
            x => x.Id == tenant.Id && x.Active,
            update,
            new FindOneAndUpdateOptions<Tenant> { ReturnDocument = ReturnDocument.After })
            ?? throw new InvalidOperationException($"Tenant '{tenant.Id}' não encontrado ou inativo durante migração de chave RSA.");
    }

    private TenantSigningKeyMaterial ToMaterial(Tenant tenant)
    {
        if (!HasSigningKey(tenant))
            throw new InvalidOperationException($"Tenant '{tenant.Id}' não possui chave RSA de assinatura.");

        return new TenantSigningKeyMaterial(
            _protector.Unprotect(tenant.SigningPrivateKeyEncrypted!),
            tenant.SigningPublicKeyJwk!,
            tenant.SigningKeyId!,
            tenant.SigningKeyVersion);
    }

    private static bool HasSigningKey(Tenant tenant) =>
        !string.IsNullOrWhiteSpace(tenant.SigningPrivateKeyEncrypted)
        && !string.IsNullOrWhiteSpace(tenant.SigningPublicKeyJwk)
        && !string.IsNullOrWhiteSpace(tenant.SigningKeyId);
}
