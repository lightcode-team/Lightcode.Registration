using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.Contracts.Accounts;

/// <summary>Payload de sucesso do registo: identificador da conta criada na coleção Users.</summary>
public sealed record RegisterAccountResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("schemaId")] string SchemaId,
    [property: JsonPropertyName("requiresEmailConfirmation")] bool RequiresEmailConfirmation = false,
    [property: JsonPropertyName("confirmationUrl")] string? ConfirmationUrl = null);
