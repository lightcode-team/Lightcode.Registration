namespace Lightcode.Registration.Application.Contracts.JsonSchema;

public sealed record CreateAccountJsonSchemaRequest(
    string Key,
    string? DisplayName,
    string SchemaJson,
    bool IsDefault);
