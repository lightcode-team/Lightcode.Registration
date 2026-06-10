namespace Lightcode.Registration.Application.SchemaConfig;

/// <summary>Modo de validação contra um JSON Schema de conta.</summary>
public enum JsonSchemaValidationMode
{
    /// <summary>Valida todos os campos, incluindo <c>required</c>.</summary>
    Full,

    /// <summary>
    /// Ignora <c>required</c> apenas na raiz do schema (cadastro por steps).
    /// Objetos enviados são validados por completo, incluindo <c>required</c> aninhado.
    /// </summary>
    Partial
}
