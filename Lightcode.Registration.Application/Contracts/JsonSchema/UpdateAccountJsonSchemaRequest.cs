namespace Lightcode.Registration.Application.Contracts.JsonSchema;

public sealed record UpdateAccountJsonSchemaRequest(
    string? DisplayName,
    string? SchemaJson,
    bool? IsDefault);
