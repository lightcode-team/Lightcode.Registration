namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Valida uma instância JSON contra um schema JSON (draft).</summary>
public interface IJsonSchemaValidationService
{
    /// <summary>Lista vazia se válido; senão mensagens de erro.</summary>
    IReadOnlyList<string> Validate(string schemaJson, string instanceJson);
}
