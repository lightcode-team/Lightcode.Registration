using System.Text.Json.Serialization;

namespace Lightcode.Registration.Api.Models;

/// <summary>Envelope padrão de resposta da API.</summary>
public sealed class ApiEnvelope<T>
{
    [JsonPropertyName("Erro")]
    public bool Erro { get; init; }

    [JsonPropertyName("Erros")]
    public IReadOnlyList<string> Erros { get; init; } = [];

    [JsonPropertyName("StatusCode")]
    public int StatusCode { get; init; }

    [JsonPropertyName("Data")]
    public T? Data { get; init; }
}
