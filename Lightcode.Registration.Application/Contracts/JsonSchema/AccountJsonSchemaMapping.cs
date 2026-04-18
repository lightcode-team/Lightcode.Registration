using System.Text.Json;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Contracts.JsonSchema;

/// <summary>Converte entre <see cref="JsonElement"/> (API) e texto JSON persistido na entidade.</summary>
public static class AccountJsonSchemaMapping
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static string ToStoredJson(JsonElement schema) => JsonSerializer.Serialize(schema, SerializerOptions);

    public static string? ToStoredConfigJson(JsonElement? config)
    {
        if (!config.HasValue)
            return null;
        var el = config.Value;
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return JsonSerializer.Serialize(el, SerializerOptions);
    }

    public static JsonElement ToApiElement(AccountJsonSchema entity)
    {
        using var doc = JsonDocument.Parse(entity.SchemaJson);
        return doc.RootElement.Clone();
    }

    public static JsonElement? ToApiConfigElement(AccountJsonSchema entity)
    {
        if (string.IsNullOrWhiteSpace(entity.ConfigJson))
            return null;
        using var doc = JsonDocument.Parse(entity.ConfigJson!);
        return doc.RootElement.Clone();
    }
}
