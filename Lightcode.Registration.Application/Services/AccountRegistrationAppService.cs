using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;
using Lightcode.Registration.Application.SchemaConfig;
using Lightcode.Registration.Application.Security;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountRegistrationAppService(
    ITenantLookup tenantLookup,
    IAccountJsonSchemaRepository schemaRepository,
    IJsonSchemaValidationService jsonSchemaValidation,
    IUserAccountWriter userAccountWriter,
    IPasswordHasher passwordHasher) : IAccountRegistrationAppService
{
    public async Task<ServiceResult<RegisterAccountResult>> RegisterAsync(
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

        obj.Remove("confirmationReturnUrl");
        obj.Remove("role");
        obj.Remove("roles");

        var cleanedJson = obj.ToJsonString();
        var errors = jsonSchemaValidation.Validate(
            schemaEntity.SchemaJson,
            cleanedJson,
            JsonSchemaValidationMode.Partial);
        if (errors.Count > 0)
            return ServiceResult<RegisterAccountResult>.Fail(400, errors.ToArray());

        if (obj["email"] is not JsonValue emailNode || !emailNode.TryGetValue<string>(out var email) || string.IsNullOrWhiteSpace(email))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo email em falta ou inválido.");

        if (obj["username"] is not JsonValue userNode || !userNode.TryGetValue<string>(out var username) || string.IsNullOrWhiteSpace(username))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo username em falta ou inválido.");

        if (obj["password"] is not JsonValue passNode || !passNode.TryGetValue<string>(out var plain) || string.IsNullOrWhiteSpace(plain))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo password em falta ou inválido.");

        email = email.Trim().ToLowerInvariant();
        username = username.Trim().ToLowerInvariant();

        var config = schemaEntity.GetConfig();
        if (config.ValidateDuplicateEmail
            && await userAccountWriter.EmailExistsAsync(tenant.Id, email, cancellationToken))
            return ServiceResult<RegisterAccountResult>.Fail(409, "Já existe uma conta com este email.");

        if (await userAccountWriter.UsernameExistsAsync(tenant.Id, username, cancellationToken))
            return ServiceResult<RegisterAccountResult>.Fail(409, "Já existe uma conta com este nome de utilizador.");

        obj[AccountUserFields.SchemaId] = schemaId;
        obj["password"] = passwordHasher.Hash(plain);
        obj["email"] = email;
        obj["username"] = username;
        obj["roles"] = new JsonArray(JsonValue.Create(UserRoles.User));
        obj["createdAtUtc"] = JsonValue.Create(DateTime.UtcNow);

        obj["status"] = JsonValue.Create(AccountStatuses.Incomplete);

        var toSave = obj.ToJsonString();
        var userId = await userAccountWriter.InsertAsync(tenant.Id, toSave, cancellationToken);

        return ServiceResult<RegisterAccountResult>.Ok(
            new RegisterAccountResult(userId, schemaId),
            201,
            "Conta criada. Complete o registo para ativar.");
    }
}
