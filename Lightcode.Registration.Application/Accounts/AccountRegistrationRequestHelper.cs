using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Accounts;

public static class AccountRegistrationRequestHelper
{
    public static bool TryExtractSchemaId(JsonObject obj, out string schemaId, out string? error)
    {
        schemaId = string.Empty;
        error = null;

        if (obj[AccountUserFields.SchemaId] is not JsonValue node
            || !node.TryGetValue<string>(out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            error = "schemaId é obrigatório.";
            return false;
        }

        schemaId = value.Trim();
        obj.Remove(AccountUserFields.SchemaId);
        return true;
    }

    public static async Task<ServiceResult<AccountJsonSchema>> ResolveSchemaAsync(
        IAccountJsonSchemaRepository repository,
        string tenantId,
        string schemaId,
        CancellationToken cancellationToken)
    {
        var schema = await repository.GetByIdAsync(tenantId, schemaId, cancellationToken);
        if (schema is null)
            return ServiceResult<AccountJsonSchema>.Fail(404, "Schema de conta não encontrado.");

        return ServiceResult<AccountJsonSchema>.Ok(schema);
    }

    public static async Task<AccountJsonSchema?> ResolveSchemaForUserAsync(
        IAccountJsonSchemaRepository repository,
        string tenantId,
        JsonObject userDocument,
        CancellationToken cancellationToken)
    {
        var schemaId = UserAccountApiSanitizer.GetSchemaId(userDocument);
        if (!string.IsNullOrWhiteSpace(schemaId))
            return await repository.GetByIdAsync(tenantId, schemaId.Trim(), cancellationToken);

        return await repository.GetDefaultAsync(tenantId, cancellationToken);
    }
}
