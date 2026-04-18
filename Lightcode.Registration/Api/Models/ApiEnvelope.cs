using System.Text.Json.Serialization;

namespace Lightcode.Registration.Api.Models;

/// <summary>Envelope padrão de resposta da API.</summary>
public sealed class ApiEnvelope<T>
{
    [JsonPropertyName("Error")]
    public bool Error { get; init; }

    [JsonPropertyName("Errors")]
    public IReadOnlyList<string> Errors { get; init; } = [];

    [JsonPropertyName("StatusCode")]
    public int StatusCode { get; init; }

    /// <summary>Mensagem opcional fora de <see cref="Data"/> (ex.: confirmação de sucesso).</summary>
    [JsonPropertyName("Message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonPropertyName("Data")]
    public T? Data { get; init; }
}
