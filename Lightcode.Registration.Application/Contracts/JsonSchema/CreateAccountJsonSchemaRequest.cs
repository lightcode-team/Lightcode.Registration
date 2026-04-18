using System.Text.Json;

namespace Lightcode.Registration.Application.Contracts.JsonSchema;

/// <summary>Schema como objeto JSON (não como string escapada).</summary>
public sealed record CreateAccountJsonSchemaRequest(
    string Key,
    string? DisplayName,
    JsonElement? Config,
    JsonElement SchemaJson,
    bool IsDefault);
