namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Aplica o validador Mongo ($jsonSchema) na coleção Users do tenant.</summary>
public interface IUsersCollectionSchemaApplier
{
    Task ApplyAsync(string tenantId, string mongoJsonSchemaDocumentJson, CancellationToken cancellationToken = default);
}
