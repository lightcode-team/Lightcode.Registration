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
    IPasswordHasher passwordHasher,
    AccountRegistrationTwoFactorSupport twoFactorSupport) : IAccountAdminAppService
{
    public async Task<ServiceResult<RegisterAccountResult>> RegisterByAdminAsync(
        string tenantId,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<RegisterAccountResult>.Fail(400, "TenantId é obrigatório.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<RegisterAccountResult>.Fail(404, "Tenant não encontrado ou inativo.");

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(requestJson);
        }
        catch
        {
            return ServiceResult<RegisterAccountResult>.Fail(400, "Corpo JSON inválido.");
        }

        if (root is not JsonObject obj)
            return ServiceResult<RegisterAccountResult>.Fail(400, "O registo deve ser um objeto JSON.");

        if (!AccountRegistrationRequestHelper.TryExtractSchemaId(obj, out var schemaId, out var schemaIdError))
            return ServiceResult<RegisterAccountResult>.Fail(400, schemaIdError!);

        var schemaResult = await AccountRegistrationRequestHelper.ResolveSchemaAsync(
            schemaRepository,
            tenant.Id,
            schemaId,
            cancellationToken);
        if (!schemaResult.IsSuccess)
            return ServiceResult<RegisterAccountResult>.Fail(schemaResult.StatusCode, schemaResult.Errors.ToArray());

        var schemaEntity = schemaResult.Value!;

        string? confirmationReturnUrl = null;
        if (obj["confirmationReturnUrl"] is JsonValue returnUrlNode
            && returnUrlNode.TryGetValue<string>(out var returnUrl)
            && !string.IsNullOrWhiteSpace(returnUrl))
            confirmationReturnUrl = returnUrl.Trim();

        obj.Remove("confirmationReturnUrl");
        obj.Remove("scope");
        obj.Remove("role");

        var validationJson = obj.ToJsonString();
        var validationErrors = jsonSchemaValidation.Validate(schemaEntity.SchemaJson, validationJson);
        if (validationErrors.Count > 0)
            return ServiceResult<RegisterAccountResult>.Fail(400, validationErrors.ToArray());

        if (obj["email"] is not JsonValue emailNode || !emailNode.TryGetValue<string>(out var email) || string.IsNullOrWhiteSpace(email))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo email em falta ou inválido.");

        if (obj["username"] is not JsonValue userNode || !userNode.TryGetValue<string>(out var username) || string.IsNullOrWhiteSpace(username))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo username em falta ou inválido.");

        if (obj["password"] is not JsonValue passNode || !passNode.TryGetValue<string>(out var plain) || string.IsNullOrWhiteSpace(plain))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo password em falta ou inválido.");

        if (obj["roles"] is not JsonArray rolesNode || rolesNode.Count == 0)
            return ServiceResult<RegisterAccountResult>.Fail(400, "Roles é obrigatório e deve conter pelo menos um valor.");

        var rawRoles = rolesNode
            .Where(n => n is JsonValue v && v.TryGetValue<string>(out _))
            .Select(n => ((JsonValue)n!).GetValue<string>()!)
            .ToList();

        email = email.Trim().ToLowerInvariant();
        username = username.Trim().ToLowerInvariant();
        var roles = UserRoles.NormalizeAccountRoles(rawRoles);

        var config = schemaEntity.GetConfig();
        if (config.ValidateDuplicateEmail && await userAccountWriter.EmailExistsAsync(tenant.Id, email, cancellationToken))
            return ServiceResult<RegisterAccountResult>.Fail(409, "Já existe uma conta com este email.");

        if (await userAccountWriter.UsernameExistsAsync(tenant.Id, username, cancellationToken))
            return ServiceResult<RegisterAccountResult>.Fail(409, "Já existe uma conta com este nome de utilizador.");

        obj[AccountUserFields.SchemaId] = schemaId;
        obj["email"] = email;
        obj["username"] = username;
        obj["password"] = passwordHasher.Hash(plain);
        obj["roles"] = new JsonArray(roles.Select(r => JsonValue.Create(r)!).ToArray());
        obj["createdAtUtc"] = JsonValue.Create(DateTime.UtcNow);

        if (AccountSchemaConfigParser.TryGetRegistrationExpiry(config, out var daysExpiry))
            obj["registrationExpiresAtUtc"] = JsonValue.Create(DateTime.UtcNow.AddDays(daysExpiry));

        var twoFactorResult = await twoFactorSupport.ApplyAsync(
            obj,
            config,
            tenant.Id,
            email,
            username,
            confirmationReturnUrl,
            cancellationToken);

        var toSave = obj.ToJsonString();
        var userId = await userAccountWriter.InsertAsync(tenant.Id, toSave, cancellationToken);

        var message = twoFactorResult.RequiresEmailConfirmation
            ? "Conta criada. Confirme o email para ativar."
            : "Conta criada com sucesso.";

        return ServiceResult<RegisterAccountResult>.Ok(
            new RegisterAccountResult(
                userId,
                schemaId,
                twoFactorResult.RequiresEmailConfirmation,
                twoFactorResult.ConfirmationUrl),
            201,
            message);
    }

    public async Task<ServiceResult<IReadOnlyList<UserAccountListItemDto>>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<IReadOnlyList<UserAccountListItemDto>>.Fail(400, "TenantId é obrigatório.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<IReadOnlyList<UserAccountListItemDto>>.Fail(404, "Tenant não encontrado ou inativo.");

        var documents = await userAccountWriter.ListUserDocumentsJsonAsync(tenant.Id, cancellationToken);
        var items = new List<UserAccountListItemDto>();

        foreach (var json in documents)
        {
            if (JsonNode.Parse(json) is not JsonObject obj)
                continue;

            var item = UserAccountApiSanitizer.ToListItem(obj);
            if (item is not null)
                items.Add(item);
        }

        return ServiceResult<IReadOnlyList<UserAccountListItemDto>>.Ok(items);
    }

    public async Task<ServiceResult<UserAccountDetailDto>> GetByIdAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
            return ServiceResult<UserAccountDetailDto>.Fail(400, "Tenant e userId são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<UserAccountDetailDto>.Fail(404, "Tenant não encontrado ou inativo.");

        var json = await userAccountWriter.GetUserDocumentJsonAsync(tenant.Id, userId.Trim(), cancellationToken);
        if (json is null)
            return ServiceResult<UserAccountDetailDto>.Fail(404, "Conta não encontrada.");

        if (JsonNode.Parse(json) is not JsonObject obj)
            return ServiceResult<UserAccountDetailDto>.Fail(400, "Documento de utilizador inválido.");

        var id = UserAccountApiSanitizer.ResolveDocumentId(obj);
        if (string.IsNullOrWhiteSpace(id))
            return ServiceResult<UserAccountDetailDto>.Fail(400, "Documento de utilizador inválido.");

        return ServiceResult<UserAccountDetailDto>.Ok(new UserAccountDetailDto(
            id,
            UserAccountApiSanitizer.GetSchemaId(obj) ?? string.Empty,
            UserAccountApiSanitizer.ToPublicProfile(obj)));
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

        var schemaEntity = await AccountRegistrationRequestHelper.ResolveSchemaForUserAsync(
            schemaRepository,
            tenant.Id,
            existingObj,
            cancellationToken);
        if (schemaEntity is null)
            return ServiceResult<UpdateAccountRolesResult>.Fail(400, "Schema de conta não encontrado para este utilizador.");

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
