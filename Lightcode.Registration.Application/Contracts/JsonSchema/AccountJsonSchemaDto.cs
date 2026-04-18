using System.Text.Json;

namespace Lightcode.Registration.Application.Contracts.JsonSchema;

public sealed record AccountJsonSchemaDto(
    string Id,
    string TenantId,
    string Key,
    string? DisplayName,
    JsonElement? Config,
    JsonElement SchemaJson,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
