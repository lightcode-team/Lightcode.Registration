using System.Text.Json;
using Lightcode.Registration.Application.Abstractions;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

/// <summary>Conversão simples de JSON Schema (type/properties) para o formato esperado por <c>$jsonSchema</c> do MongoDB.</summary>
public sealed class JsonSchemaDraftToMongoValidatorMapper : IJsonSchemaToMongoValidatorMapper
{
    public string? TryMap(string draftSchemaJson, out IReadOnlyList<string> errors)
    {
        errors = Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(draftSchemaJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors = ["O schema deve ser um objeto na raiz."];
                return null;
            }

            var converted = MapObject(root);
            return JsonSerializer.Serialize(converted);
        }
        catch (Exception ex)
        {
            errors = [$"Falha ao mapear schema para Mongo: {ex.Message}"];
            return null;
        }
    }

    private static Dictionary<string, object?> MapObject(JsonElement el)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["bsonType"] = "object"
        };

        // required é validado na aplicação (complete-register); o Mongo aceita documentos parciais.

        if (el.TryGetProperty("additionalProperties", out var add))
            result["additionalProperties"] = add.ValueKind == JsonValueKind.True || add.ValueKind == JsonValueKind.False
                ? add.GetBoolean()
                : MapObject(add);

        if (el.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var p in props.EnumerateObject())
                dict[p.Name] = MapSchema(p.Value);
            result["properties"] = dict;
        }

        return result;
    }

    private static object? MapSchema(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        if (el.TryGetProperty("type", out var typeEl))
        {
            var t = typeEl.GetString();
            return t switch
            {
                "object" => MapObject(el),
                "string" => MapScalar("string"),
                "number" => MapScalar("double"),
                "integer" => MapScalar("int"),
                "boolean" => MapScalar("bool"),
                "array" => MapArray(el),
                _ => MapScalar("string")
            };
        }

        return MapScalar("string");
    }

    private static Dictionary<string, object?> MapScalar(string bsonType) =>
        new() { ["bsonType"] = bsonType };

    private static Dictionary<string, object?> MapArray(JsonElement el)
    {
        var o = new Dictionary<string, object?> { ["bsonType"] = "array" };
        if (el.TryGetProperty("items", out var items))
            o["items"] = MapSchema(items);
        return o;
    }
}
