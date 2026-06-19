using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class MongoUserTwoFactorSettingsService(
    IMongoClient client,
    ITenantLookup tenantLookup,
    IPlatformAdminRepository platformAdminRepository) : ITwoFactorSettingsService
{
    public async Task<UserTwoFactorSettings> GetUserSettingsAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var doc = await GetUserDocumentAsync(tenantId, userId, cancellationToken);
        if (doc is null
            || !doc.TryGetValue(AccountSecurityReservedFields.TwoFactorSettings, out var settingsValue)
            || !settingsValue.IsBsonDocument)
            return new UserTwoFactorSettings(false, TwoFactorMethods.EmailCode, false);

        var settings = settingsValue.AsBsonDocument;
        var enabled = settings.TryGetValue("enabled", out var enabledValue) && enabledValue.ToBoolean();
        var emailEnabled = settings.TryGetValue("emailEnabled", out var emailEnabledValue) && emailEnabledValue.ToBoolean();
        var preferred = settings.TryGetValue("preferredMethod", out var preferredValue) && preferredValue.IsString
            ? preferredValue.AsString
            : TwoFactorMethods.EmailCode;

        return new UserTwoFactorSettings(enabled, preferred, emailEnabled);
    }

    public async Task SetUserEmailTwoFactorAsync(
        string tenantId,
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(userId, out var oid))
            throw new ArgumentException("userId inválido.", nameof(userId));

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant não encontrado.");

        var collection = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var update = Builders<BsonDocument>.Update
            .Set($"{AccountSecurityReservedFields.TwoFactorSettings}.enabled", enabled)
            .Set($"{AccountSecurityReservedFields.TwoFactorSettings}.emailEnabled", enabled)
            .Set($"{AccountSecurityReservedFields.TwoFactorSettings}.preferredMethod", TwoFactorMethods.EmailCode)
            .Set($"{AccountSecurityReservedFields.TwoFactorSettings}.updatedAtUtc", DateTime.UtcNow);

        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", oid),
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<UserTwoFactorSettings> GetPlatformAdminSettingsAsync(
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var admin = await platformAdminRepository.GetAdminByIdAsync(adminId, cancellationToken);
        if (admin?.TwoFactorSettings is null)
            return new UserTwoFactorSettings(false, TwoFactorMethods.EmailCode, false);

        return new UserTwoFactorSettings(
            admin.TwoFactorSettings.Enabled,
            string.IsNullOrWhiteSpace(admin.TwoFactorSettings.PreferredMethod)
                ? TwoFactorMethods.EmailCode
                : admin.TwoFactorSettings.PreferredMethod,
            admin.TwoFactorSettings.EmailEnabled);
    }

    public async Task SetPlatformAdminEmailTwoFactorAsync(
        string adminId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var admin = await platformAdminRepository.GetAdminByIdAsync(adminId, cancellationToken)
            ?? throw new InvalidOperationException("Administrador não encontrado.");

        admin.TwoFactorSettings = new PlatformAdminTwoFactorSettings
        {
            Enabled = enabled,
            EmailEnabled = enabled,
            PreferredMethod = TwoFactorMethods.EmailCode,
            UpdatedAtUtc = DateTime.UtcNow
        };
        admin.UpdatedAtUtc = DateTime.UtcNow;

        await platformAdminRepository.ReplaceAdminAsync(admin, cancellationToken);
    }

    private async Task<BsonDocument?> GetUserDocumentAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(userId, out var oid))
            return null;

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return null;

        var collection = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        return await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", oid))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
