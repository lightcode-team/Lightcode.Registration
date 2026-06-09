using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.Contracts.Accounts;

public sealed record UserAccountListItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("schemaId")] string SchemaId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("createdAtUtc")] DateTime? CreatedAtUtc);

public sealed record UserAccountDetailDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("schemaId")] string SchemaId,
    [property: JsonPropertyName("profile")] JsonElement Profile);
