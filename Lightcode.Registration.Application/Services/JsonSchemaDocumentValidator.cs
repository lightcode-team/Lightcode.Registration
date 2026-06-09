using System.Text.Json.Nodes;
using Json.Schema;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.SchemaConfig;

namespace Lightcode.Registration.Application.Services;

public sealed class JsonSchemaDocumentValidator : IJsonSchemaValidationService
{
    public IReadOnlyList<string> Validate(
        string schemaJson,
        string instanceJson,
        JsonSchemaValidationMode mode = JsonSchemaValidationMode.Full)
    {
        if (mode == JsonSchemaValidationMode.Partial)
            schemaJson = StripRequiredConstraints(schemaJson);

        JsonSchema schema;
        try
        {
            schema = JsonSchema.FromText(schemaJson);
        }
        catch (Exception ex)
        {
            return [$"Schema inválido: {ex.Message}"];
        }

        JsonNode? instance;
        try
        {
            instance = JsonNode.Parse(instanceJson);
        }
        catch (Exception ex)
        {
            return [$"JSON do pedido inválido: {ex.Message}"];
        }

        var options = new EvaluationOptions
        {
            RequireFormatValidation = true,
            OutputFormat = OutputFormat.List
        };
        var result = schema.Evaluate(instance, options);

        if (result.IsValid)
            return [];

        var messages = new List<string>();
        CollectErrors(result, messages);
        return messages.Count > 0 ? messages : ["A instância não cumpre o schema."];
    }

    private static void CollectErrors(EvaluationResults node, List<string> messages)
    {
        if (node.Errors is { Count: > 0 } errors)
        {
            foreach (var kv in errors)
                messages.Add($"{node.InstanceLocation}: {kv.Key} — {kv.Value}");
        }

        if (!node.HasDetails)
            return;

        foreach (var detail in node.Details)
            CollectErrors(detail, messages);
    }

    /// <summary>
    /// Remove apenas <c>required</c> da raiz do schema.
    /// Objetos aninhados mantêm o seu <c>required</c>: se o cliente enviar o objeto, as props obrigatórias são validadas.
    /// </summary>
    private static string StripRequiredConstraints(string schemaJson)
    {
        var node = JsonNode.Parse(schemaJson);
        if (node is JsonObject root)
            root.Remove("required");

        return node!.ToJsonString();
    }
}
