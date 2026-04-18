namespace Lightcode.Registration.Application.Contracts.JsonSchema;

public sealed record AccountJsonSchemaDto(
    string Id,
    string TenantId,
    string Key,
    string? DisplayName,
    string SchemaJson,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
