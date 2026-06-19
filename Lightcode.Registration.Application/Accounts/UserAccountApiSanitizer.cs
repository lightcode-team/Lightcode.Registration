using System.Text.Json;
using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Contracts.Accounts;

namespace Lightcode.Registration.Application.Accounts;

public static class UserAccountApiSanitizer
{
    private static readonly HashSet<string> SensitiveFields =
    [
        "password",
        AccountEmailConfirmationFields.SecretHash,
        AccountEmailConfirmationFields.ExpiresAtUtc,
        AccountSecurityReservedFields.TwoFactorSettings,
        "twoFactor",
        "mfa",
        "totpSecret",
        "totpSecretEncrypted",
        "recoveryCodes",
        "trustedDevices",
        "twoFactorEnabled"
    ];

    public static JsonElement ToPublicProfile(JsonObject document)
    {
        var clone = JsonNode.Parse(document.ToJsonString()) as JsonObject
            ?? throw new InvalidOperationException("Falha ao clonar documento de utilizador.");

        foreach (var field in SensitiveFields)
            clone.Remove(field);

        return clone.Deserialize<JsonElement>();
    }

    public static string? GetSchemaId(JsonObject document) =>
        document[AccountUserFields.SchemaId] is JsonValue node && node.TryGetValue<string>(out var value)
            ? value
            : null;

    public static string ResolveDocumentId(JsonObject document)
    {
        if (document["_id"] is JsonValue idValue && idValue.TryGetValue<string>(out var id))
            return id;

        if (document["_id"] is JsonObject oidWrapper
            && oidWrapper["$oid"] is JsonValue oidValue
            && oidValue.TryGetValue<string>(out var oid))
            return oid;

        return string.Empty;
    }

    public static UserAccountListItemDto? ToListItem(JsonObject document)
    {
        var id = ResolveDocumentId(document);
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var email = document["email"] is JsonValue e && e.TryGetValue<string>(out var ev) ? ev : string.Empty;
        var username = document["username"] is JsonValue u && u.TryGetValue<string>(out var uv) ? uv : string.Empty;
        var status = document["status"] is JsonValue s && s.TryGetValue<string>(out var sv) ? sv : null;
        DateTime? createdAt = document["createdAtUtc"] is JsonValue c && c.TryGetValue<DateTime>(out var cv) ? cv : null;

        return new UserAccountListItemDto(
            id,
            GetSchemaId(document) ?? string.Empty,
            email,
            username,
            status,
            createdAt);
    }
}
