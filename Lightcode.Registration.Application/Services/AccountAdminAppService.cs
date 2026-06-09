using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;
using Lightcode.Registration.Application.SchemaConfig;
using Lightcode.Registration.Application.Security;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountAdminAppService(
    ITenantLookup tenantLookup,
    IAccountJsonSchemaRepository schemaRepository,
    IJsonSchemaValidationService jsonSchemaValidation,
    IUserAccountWriter userAccountWriter,
    IPasswordHasher passwordHasher) : IAccountAdminAppService
{
    public async Task<ServiceResult<RegisterAccountResult>> RegisterByAdminAsync(
        string tenantId,
        AdminRegisterAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<RegisterAccountResult>.Fail(400, "TenantId é obrigatório.");

        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Email, username e password são obrigatórios.");

        if (request.Roles is null || request.Roles.Count == 0)
            return ServiceResult<RegisterAccountResult>.Fail(400, "Roles é obrigatório e deve conter pelo menos um valor.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<RegisterAccountResult>.Fail(404, "Tenant não encontrado ou inativo.");

        var schemaEntity = await schemaRepository.GetDefaultAsync(tenant.Id, cancellationToken);
        if (schemaEntity is null)
            return ServiceResult<RegisterAccountResult>.Fail(400, "Não existe schema default de conta. Configure um em /api/account-json-schemas.");

        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username.Trim().ToLowerInvariant();
        var roles = UserRoles.NormalizeAccountRoles(request.Roles);

        if (await userAccountWriter.EmailExistsAsync(tenant.Id, email, cancellationToken))
            return ServiceResult<RegisterAccountResult>.Fail(409, "Já existe uma conta com este email.");

        if (await userAccountWriter.UsernameExistsAsync(tenant.Id, username, cancellationToken))
            return ServiceResult<RegisterAccountResult>.Fail(409, "Já existe uma conta com este nome de utilizador.");

        var obj = new JsonObject
        {
            ["email"] = email,
            ["username"] = username,
            ["password"] = passwordHasher.Hash(request.Password),
            ["roles"] = new JsonArray(roles.Select(r => JsonValue.Create(r)!).ToArray()),
            ["createdAtUtc"] = JsonValue.Create(DateTime.UtcNow),
            ["status"] = JsonValue.Create(AccountStatuses.Active)
        };

        if (AccountSchemaConfigParser.TryGetRegistrationExpiry(schemaEntity.ConfigJson, out var daysExpiry))
            obj["registrationExpiresAtUtc"] = JsonValue.Create(DateTime.UtcNow.AddDays(daysExpiry));

        var toSave = obj.ToJsonString();
        var errors = jsonSchemaValidation.Validate(schemaEntity.SchemaJson, toSave);
        if (errors.Count > 0)
            return ServiceResult<RegisterAccountResult>.Fail(400, errors.ToArray());

        var userId = await userAccountWriter.InsertAsync(tenant.Id, toSave, cancellationToken);
        return ServiceResult<RegisterAccountResult>.Ok(new RegisterAccountResult(userId), 201, "Conta criada com sucesso.");
    }

    public async Task<ServiceResult<UpdateAccountRolesResult>> UpdateRolesAsync(
        string tenantId,
        string userId,
        UpdateAccountRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
            return ServiceResult<UpdateAccountRolesResult>.Fail(400, "Tenant e userId são obrigatórios.");

        if (request.Roles is null || request.Roles.Count == 0)
            return ServiceResult<UpdateAccountRolesResult>.Fail(400, "Roles é obrigatório e deve conter pelo menos um valor.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<UpdateAccountRolesResult>.Fail(404, "Tenant não encontrado ou inativo.");

        var schemaEntity = await schemaRepository.GetDefaultAsync(tenant.Id, cancellationToken);
        if (schemaEntity is null)
            return ServiceResult<UpdateAccountRolesResult>.Fail(400, "Não existe schema default de conta para este tenant.");

        var existingJson = await userAccountWriter.GetUserDocumentJsonAsync(tenant.Id, userId.Trim(), cancellationToken);
        if (existingJson is null)
            return ServiceResult<UpdateAccountRolesResult>.Fail(404, "Conta não encontrada.");

        JsonObject? existingObj;
        try
        {
            existingObj = JsonNode.Parse(existingJson) as JsonObject;
        }
        catch
        {
            return ServiceResult<UpdateAccountRolesResult>.Fail(400, "Documento de utilizador inválido.");
        }

        if (existingObj is null)
            return ServiceResult<UpdateAccountRolesResult>.Fail(400, "Documento de utilizador inválido.");

        var roles = UserRoles.NormalizeAccountRoles(request.Roles);
        existingObj["roles"] = new JsonArray(roles.Select(r => JsonValue.Create(r)!).ToArray());

        var validationJson = existingObj.ToJsonString();
        var errors = jsonSchemaValidation.Validate(schemaEntity.SchemaJson, validationJson);
        if (errors.Count > 0)
            return ServiceResult<UpdateAccountRolesResult>.Fail(400, errors.ToArray());

        await userAccountWriter.ReplaceUserDocumentAsync(tenant.Id, userId.Trim(), validationJson, cancellationToken);
        return ServiceResult<UpdateAccountRolesResult>.Ok(new UpdateAccountRolesResult(roles));
    }
}
