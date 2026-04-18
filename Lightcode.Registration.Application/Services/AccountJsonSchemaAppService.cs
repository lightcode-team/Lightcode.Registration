using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.JsonSchema;
using Lightcode.Registration.Application.SchemaConfig;
using Lightcode.Registration.Domain.Entities;
using Json.Schema;
using System.Text.Json;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountJsonSchemaAppService(
    IAccountJsonSchemaRepository repository,
    IJsonSchemaToMongoValidatorMapper mongoMapper,
    IUsersCollectionSchemaApplier usersCollectionSchemaApplier) : IAccountJsonSchemaAppService
{
    public async Task<ServiceResult<IReadOnlyList<AccountJsonSchemaDto>>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var list = await repository.ListByTenantAsync(tenantId, cancellationToken);
        return ServiceResult<IReadOnlyList<AccountJsonSchemaDto>>.Ok(list.Select(Map).ToList());
    }

    public async Task<ServiceResult<AccountJsonSchemaDto>> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
            return ServiceResult<AccountJsonSchemaDto>.Fail(404, "Schema não encontrado.");

        return ServiceResult<AccountJsonSchemaDto>.Ok(Map(entity));
    }

    public async Task<ServiceResult<AccountJsonSchemaDto>> CreateAsync(
        string tenantId,
        CreateAccountJsonSchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return ServiceResult<AccountJsonSchemaDto>.Fail(400, "Key é obrigatória.");

        if (request.SchemaJson.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return ServiceResult<AccountJsonSchemaDto>.Fail(400, "schemaJson é obrigatório e deve ser um objeto JSON.");

        var schemaError = ValidateSchemaElement(request.SchemaJson);
        if (schemaError is not null)
            return ServiceResult<AccountJsonSchemaDto>.Fail(400, schemaError);

        var loginFieldsError = ValidateLoginRegistrationSchema(request.SchemaJson);
        if (loginFieldsError is not null)
            return ServiceResult<AccountJsonSchemaDto>.Fail(400, loginFieldsError);

        var storedConfig = AccountJsonSchemaMapping.ToStoredConfigJson(request.Config);
        if (!AccountSchemaConfigParser.TryParseAndValidate(storedConfig, out _, out var configErr))
            return ServiceResult<AccountJsonSchemaDto>.Fail(400, configErr ?? "config inválido.");

        var stored = AccountJsonSchemaMapping.ToStoredJson(request.SchemaJson);

        var existing = await repository.GetByKeyAsync(tenantId, request.Key.Trim(), cancellationToken);
        if (existing is not null)
            return ServiceResult<AccountJsonSchemaDto>.Fail(409, "Já existe um schema com esta Key.");

        var now = DateTime.UtcNow;
        var entity = new AccountJsonSchema
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            Key = request.Key.Trim(),
            DisplayName = request.DisplayName?.Trim(),
            ConfigJson = storedConfig,
            SchemaJson = stored,
            IsDefault = request.IsDefault,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (entity.IsDefault)
            await repository.ClearDefaultFlagForTenantAsync(tenantId, cancellationToken);

        await repository.InsertAsync(entity, cancellationToken);

        if (entity.IsDefault)
            await TryApplyMongoValidatorAsync(tenantId, entity.SchemaJson, cancellationToken);

        return ServiceResult<AccountJsonSchemaDto>.Ok(Map(entity), 201);
    }

    public async Task<ServiceResult<AccountJsonSchemaDto>> UpdateAsync(
        string tenantId,
        string id,
        UpdateAccountJsonSchemaRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
            return ServiceResult<AccountJsonSchemaDto>.Fail(404, "Schema não encontrado.");

        if (request.SchemaJson.HasValue)
        {
            var el = request.SchemaJson.Value;
            if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return ServiceResult<AccountJsonSchemaDto>.Fail(400, "schemaJson não pode ser null.");

            var schemaError = ValidateSchemaElement(el);
            if (schemaError is not null)
                return ServiceResult<AccountJsonSchemaDto>.Fail(400, schemaError);

            var loginFieldsError = ValidateLoginRegistrationSchema(el);
            if (loginFieldsError is not null)
                return ServiceResult<AccountJsonSchemaDto>.Fail(400, loginFieldsError);

            entity.SchemaJson = AccountJsonSchemaMapping.ToStoredJson(el);
        }

        if (request.Config.HasValue)
        {
            var el = request.Config.Value;
            if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                entity.ConfigJson = null;
            else
            {
                var cfgStored = AccountJsonSchemaMapping.ToStoredConfigJson(el);
                if (!AccountSchemaConfigParser.TryParseAndValidate(cfgStored, out _, out var cfgErr))
                    return ServiceResult<AccountJsonSchemaDto>.Fail(400, cfgErr ?? "config inválido.");
                entity.ConfigJson = cfgStored;
            }
        }

        if (request.DisplayName is not null)
            entity.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();

        if (request.IsDefault == true)
        {
            await repository.ClearDefaultFlagForTenantAsync(tenantId, cancellationToken);
            entity.IsDefault = true;
        }

        entity.UpdatedAtUtc = DateTime.UtcNow;
        await repository.ReplaceAsync(entity, cancellationToken);

        var effectiveDefault = await repository.GetDefaultAsync(tenantId, cancellationToken);
        if (effectiveDefault is not null && effectiveDefault.Id == entity.Id)
            await TryApplyMongoValidatorAsync(tenantId, effectiveDefault.SchemaJson, cancellationToken);

        return ServiceResult<AccountJsonSchemaDto>.Ok(Map(entity));
    }

    public async Task<ServiceResult<object?>> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
            return ServiceResult<object?>.Fail(404, "Schema não encontrado.");

        if (entity.IsDefault)
            return ServiceResult<object?>.Fail(400, "Não é possível eliminar o schema default. Defina outro como default primeiro.");

        await repository.DeleteAsync(tenantId, id, cancellationToken);
        return ServiceResult<object?>.Ok(null, 204);
    }

    private async Task TryApplyMongoValidatorAsync(string tenantId, string draftSchema, CancellationToken cancellationToken)
    {
        var mongo = mongoMapper.TryMap(draftSchema, out var mapErrors);
        if (mongo is null || mapErrors.Count > 0)
            return;

        await usersCollectionSchemaApplier.ApplyAsync(tenantId, mongo, cancellationToken);
    }

    private static string? ValidateSchemaElement(JsonElement schema)
    {
        try
        {
            var text = JsonSerializer.Serialize(schema);
            _ = JsonSchema.FromText(text);
            return null;
        }
        catch (Exception ex)
        {
            return $"schemaJson não é um JSON Schema válido: {ex.Message}";
        }
    }

    /// <summary>Garante email, username e password obrigatórios para cadastro/login.</summary>
    private static string? ValidateLoginRegistrationSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
            return "O schema na raiz deve ser um objeto.";

        if (!schema.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return "O schema deve declarar \"properties\" com email, username e password.";

        foreach (var name in new[] { "email", "username", "password" })
        {
            if (!props.TryGetProperty(name, out _))
                return $"O schema deve declarar a propriedade \"{name}\" em properties.";
        }

        if (!schema.TryGetProperty("required", out var req) || req.ValueKind != JsonValueKind.Array)
            return "O schema deve declarar o array \"required\".";

        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in req.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } name)
                required.Add(name);
        }

        foreach (var name in new[] { "email", "username", "password" })
        {
            if (!required.Contains(name))
                return $"O campo \"{name}\" deve constar em \"required\".";
        }

        return null;
    }

    private static AccountJsonSchemaDto Map(AccountJsonSchema e) =>
        new(
            e.Id,
            e.TenantId,
            e.Key,
            e.DisplayName,
            AccountJsonSchemaMapping.ToApiConfigElement(e),
            AccountJsonSchemaMapping.ToApiElement(e),
            e.IsDefault,
            e.CreatedAtUtc,
            e.UpdatedAtUtc);
}
