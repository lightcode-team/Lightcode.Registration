using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;
using Lightcode.Registration.Application.SchemaConfig;
using Lightcode.Registration.Application.Security;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountUpdateAppService(
    ITenantLookup tenantLookup,
    IAccountJsonSchemaRepository schemaRepository,
    IJsonSchemaValidationService jsonSchemaValidation,
    IUserAccountWriter userAccountWriter,
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokenRepository) : IAccountUpdateAppService
{
    private static readonly HashSet<string> BlockedPatchKeys =
    [
        "_id",
        AccountUserFields.SchemaId,
        "status",
        "registrationExpiresAtUtc",
        "expiryReminder30SentUtc",
        "expiryReminder15SentUtc"
    ];

    public async Task<ServiceResult<UpdateAccountResult>> UpdateAsync(
        string tenantId,
        string targetUserId,
        string actorUserId,
        IEnumerable<string> actorRoleClaims,
        IEnumerable<string> actorScopeClaims,
        string patchJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(targetUserId) || string.IsNullOrWhiteSpace(actorUserId))
            return ServiceResult<UpdateAccountResult>.Fail(400, "Tenant e identificadores de utilizador são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<UpdateAccountResult>.Fail(404, "Tenant não encontrado ou inativo.");

        var isAdmin = AccountAccessRules.IsAccountsAdmin(actorRoleClaims, actorScopeClaims);
        if (!isAdmin && !string.Equals(actorUserId.Trim(), targetUserId.Trim(), StringComparison.OrdinalIgnoreCase))
            return ServiceResult<UpdateAccountResult>.Fail(403, "Só pode alterar a sua própria conta ou ser administrador.");

        var existingJson = await userAccountWriter.GetUserDocumentJsonAsync(tenant.Id, targetUserId.Trim(), cancellationToken);
        if (existingJson is null)
            return ServiceResult<UpdateAccountResult>.Fail(404, "Conta não encontrada.");

        JsonNode? existingRoot;
        JsonObject? patchObj;
        try
        {
            existingRoot = JsonNode.Parse(existingJson);
            patchObj = JsonNode.Parse(patchJson) as JsonObject;
        }
        catch
        {
            return ServiceResult<UpdateAccountResult>.Fail(400, "JSON inválido.");
        }

        if (existingRoot is not JsonObject existingObj)
            return ServiceResult<UpdateAccountResult>.Fail(400, "Documento de utilizador inválido.");

        var schemaEntity = await AccountRegistrationRequestHelper.ResolveSchemaForUserAsync(
            schemaRepository,
            tenant.Id,
            existingObj,
            cancellationToken);
        if (schemaEntity is null)
            return ServiceResult<UpdateAccountResult>.Fail(400, "Schema de conta não encontrado para este utilizador.");

        if (patchObj is null || patchObj.Count == 0)
            return ServiceResult<UpdateAccountResult>.Fail(400, "Corpo de atualização deve ser um objeto com pelo menos uma propriedade.");

        var merged = JsonNode.Parse(existingObj.ToJsonString()) as JsonObject
            ?? throw new InvalidOperationException("Falha ao clonar documento.");

        var passwordFromPatchPlain = false;
        foreach (var prop in patchObj)
        {
            var key = prop.Key;
            if (BlockedPatchKeys.Contains(key) || AccountSecurityReservedFields.Names.Contains(key))
                continue;

            if (key == "createdAtUtc")
                continue;

            if (key == "roles" && !isAdmin)
                continue;

            if (key == "roles")
            {
                if (!isAdmin)
                    continue;

                if (prop.Value is not JsonArray arr)
                    return ServiceResult<UpdateAccountResult>.Fail(400, "O campo roles deve ser um array de strings.");

                var list = new List<string?>();
                foreach (var item in arr)
                {
                    if (item is JsonValue jv && jv.TryGetValue<string>(out var s))
                        list.Add(s);
                    else
                        list.Add(null);
                }

                merged["roles"] = new JsonArray(UserRoles.NormalizeAccountRoles(list).Select(r => JsonValue.Create(r)!).ToArray());
                continue;
            }

            if (prop.Value is null)
            {
                merged.Remove(key);
                continue;
            }

            if (key == "password")
            {
                if (prop.Value is not JsonValue pv || !pv.TryGetValue<string>(out var plain) || string.IsNullOrWhiteSpace(plain))
                    return ServiceResult<UpdateAccountResult>.Fail(400, "Password inválida.");

                merged["password"] = plain;
                passwordFromPatchPlain = true;
                continue;
            }

            merged[key] = prop.Value.DeepClone();
        }

        if (merged["email"] is JsonValue ev && ev.TryGetValue<string>(out var em))
            merged["email"] = em.Trim().ToLowerInvariant();

        if (merged["username"] is JsonValue uv && uv.TryGetValue<string>(out var un))
            merged["username"] = un.Trim().ToLowerInvariant();

        if (merged["email"] is JsonValue ev2 && ev2.TryGetValue<string>(out var emailNorm))
        {
            if (await userAccountWriter.EmailTakenByOtherUserAsync(tenant.Id, emailNorm, targetUserId.Trim(), cancellationToken))
                return ServiceResult<UpdateAccountResult>.Fail(409, "Já existe outra conta com este email.");
        }

        if (merged["username"] is JsonValue uv2 && uv2.TryGetValue<string>(out var userNorm))
        {
            if (await userAccountWriter.UsernameTakenByOtherUserAsync(tenant.Id, userNorm, targetUserId.Trim(), cancellationToken))
                return ServiceResult<UpdateAccountResult>.Fail(409, "Já existe outra conta com este nome de utilizador.");
        }

        var validationJson = merged.ToJsonString();
        var errors = jsonSchemaValidation.Validate(
            schemaEntity.SchemaJson,
            validationJson,
            JsonSchemaValidationMode.Partial);
        if (errors.Count > 0)
            return ServiceResult<UpdateAccountResult>.Fail(400, errors.ToArray());

        if (passwordFromPatchPlain && merged["password"] is JsonValue passVal && passVal.TryGetValue<string>(out var plainPw))
            merged["password"] = passwordHasher.Hash(plainPw);

        var currentStatus = existingObj["status"] is JsonValue statusNode && statusNode.TryGetValue<string>(out var statusValue)
            ? statusValue
            : AccountStatuses.Active;

        if (currentStatus is not AccountStatuses.PendingConfirmation and not AccountStatuses.Incomplete
            && AccountSchemaConfigParser.TryGetRegistrationExpiry(schemaEntity.ConfigJson, out var renewalDays))
        {
            merged["status"] = JsonValue.Create(AccountStatuses.Active);
            merged["registrationExpiresAtUtc"] = JsonValue.Create(DateTime.UtcNow.AddDays(renewalDays));
            merged.Remove("expiryReminder30SentUtc");
            merged.Remove("expiryReminder15SentUtc");
        }

        await userAccountWriter.ReplaceUserDocumentAsync(tenant.Id, targetUserId.Trim(), merged.ToJsonString(), cancellationToken);

        if (passwordFromPatchPlain)
            await refreshTokenRepository.RevokeBySubjectAsync(
                tenant.Id,
                targetUserId.Trim(),
                TokenSubjectTypes.User,
                cancellationToken);

        return ServiceResult<UpdateAccountResult>.Ok(new UpdateAccountResult(), 200);
    }
}
