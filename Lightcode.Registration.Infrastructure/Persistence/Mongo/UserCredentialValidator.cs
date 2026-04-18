using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class UserCredentialValidator(
    IMongoClient client,
    ITenantLookup tenantLookup,
    IPasswordHasher passwordHasher) : IUserCredentialValidator
{
    public async Task<CredentialValidationResult?> ValidateAsync(
        string tenantId,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return null;

        var coll = client.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
        var doc = await coll.Find(new BsonDocument("username", username)).FirstOrDefaultAsync(cancellationToken);
        if (doc is null || !doc.Contains("password"))
            return null;

        var stored = doc["password"].IsString ? doc["password"].AsString : null;
        if (string.IsNullOrEmpty(stored) || !passwordHasher.Verify(password, stored))
            return null;

        if (doc.Contains("status") && doc["status"].IsString && doc["status"].AsString == AccountStatuses.Expired)
            return null;

        if (doc.Contains("registrationExpiresAtUtc") && doc["registrationExpiresAtUtc"].IsValidDateTime)
        {
            var expiresUtc = doc["registrationExpiresAtUtc"].ToUniversalTime();
            if (expiresUtc < DateTime.UtcNow)
                return null;
        }

        var userId = doc["_id"].ToString()!;
        var roles = ReadRolesFromUserDocument(doc);

        return new CredentialValidationResult(userId, roles);
    }

    private static IReadOnlyList<string> ReadRolesFromUserDocument(BsonDocument doc)
    {
        if (doc.Contains("roles") && doc["roles"].IsBsonArray)
        {
            var raw = doc["roles"].AsBsonArray
                .Where(e => e.IsString)
                .Select(e => e.AsString)
                .ToList();
            return UserRoles.NormalizeMany(raw);
        }

        if (doc.Contains("role") && doc["role"].IsString)
            return UserRoles.NormalizeMany([doc["role"].AsString]);

        return UserRoles.NormalizeMany(null);
    }
}
