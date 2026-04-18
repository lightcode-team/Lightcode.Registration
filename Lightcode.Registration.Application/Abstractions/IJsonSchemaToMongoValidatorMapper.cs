namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Mapeia JSON Schema (draft) para o subconjunto aceite por <c>$jsonSchema</c> do MongoDB.</summary>
public interface IJsonSchemaToMongoValidatorMapper
{
    /// <returns>JSON do objeto interno de <c>$jsonSchema</c> ou null se inválido.</returns>
    string? TryMap(string draftSchemaJson, out IReadOnlyList<string> errors);
}
