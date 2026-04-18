using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;

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

        var schemaEntity = await schemaRepository.GetDefaultAsync(tenant.Id, cancellationToken);
        if (schemaEntity is null)
            return ServiceResult<RegisterAccountResult>.Fail(400, "Não existe schema default de conta. Configure um em /api/account-json-schemas.");

        var errors = jsonSchemaValidation.Validate(schemaEntity.SchemaJson, requestJson);
        if (errors.Count > 0)
            return ServiceResult<RegisterAccountResult>.Fail(400, errors.ToArray());

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

        if (obj["email"] is not JsonValue emailNode || !emailNode.TryGetValue<string>(out var email) || string.IsNullOrWhiteSpace(email))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo email em falta ou inválido.");

        email = email.Trim().ToLowerInvariant();
        if (await userAccountWriter.EmailExistsAsync(tenant.Id, email, cancellationToken))
            return ServiceResult<RegisterAccountResult>.Fail(409, "Já existe uma conta com este email.");

        if (obj["password"] is JsonValue pv && pv.TryGetValue<string>(out var plain) && !string.IsNullOrEmpty(plain))
            obj["password"] = passwordHasher.Hash(plain);

        obj["email"] = email;
        obj["createdAtUtc"] = JsonValue.Create(DateTime.UtcNow);

        var toSave = obj.ToJsonString();
        await userAccountWriter.InsertAsync(tenant.Id, toSave, cancellationToken);

        return ServiceResult<RegisterAccountResult>.Ok(new RegisterAccountResult("Conta criada com sucesso."), 201);
    }
}
