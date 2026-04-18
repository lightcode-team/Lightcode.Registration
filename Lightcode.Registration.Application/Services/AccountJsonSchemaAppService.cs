using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.JsonSchema;
using Lightcode.Registration.Domain.Entities;
using Json.Schema;

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

        if (string.IsNullOrWhiteSpace(request.SchemaJson))
            return ServiceResult<AccountJsonSchemaDto>.Fail(400, "SchemaJson é obrigatório.");

        var schemaError = ValidateSchemaText(request.SchemaJson);
        if (schemaError is not null)
            return ServiceResult<AccountJsonSchemaDto>.Fail(400, schemaError);

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
            SchemaJson = request.SchemaJson.Trim(),
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

        if (request.SchemaJson is not null)
        {
            if (string.IsNullOrWhiteSpace(request.SchemaJson))
                return ServiceResult<AccountJsonSchemaDto>.Fail(400, "SchemaJson não pode ser vazio.");

            var schemaError = ValidateSchemaText(request.SchemaJson);
            if (schemaError is not null)
                return ServiceResult<AccountJsonSchemaDto>.Fail(400, schemaError);

            entity.SchemaJson = request.SchemaJson.Trim();
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

    private static string? ValidateSchemaText(string schemaJson)
    {
        try
        {
            _ = JsonSchema.FromText(schemaJson);
            return null;
        }
        catch (Exception ex)
        {
            return $"SchemaJson não é um JSON Schema válido: {ex.Message}";
        }
    }

    private static AccountJsonSchemaDto Map(AccountJsonSchema e) =>
        new(e.Id, e.TenantId, e.Key, e.DisplayName, e.SchemaJson, e.IsDefault, e.CreatedAtUtc, e.UpdatedAtUtc);
}
