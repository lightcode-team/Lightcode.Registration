using System.Text.Json;

namespace Lightcode.Registration.Application.Contracts.JsonSchema;

public sealed record UpdateAccountJsonSchemaRequest(
    string? DisplayName,
    JsonElement? Config,
    JsonElement? SchemaJson,
    bool? IsDefault);
