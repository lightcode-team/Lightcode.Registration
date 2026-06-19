using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;
using Lightcode.Registration.Application.SchemaConfig;
using Lightcode.Registration.Application.Security;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountCompleteRegistrationAppService(
    ITenantLookup tenantLookup,
    IAccountJsonSchemaRepository schemaRepository,
    IJsonSchemaValidationService jsonSchemaValidation,
    IUserAccountWriter userAccountWriter,
    AccountRegistrationTwoFactorSupport twoFactorSupport) : IAccountCompleteRegistrationAppService
{
    public async Task<ServiceResult<RegisterAccountResult>> CompleteRegisterAsync(
        string tenantId,
        string targetUserId,
        string actorUserId,
        IEnumerable<string> actorRoleClaims,
        IEnumerable<string> actorScopeClaims,
        CompleteRegisterRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(targetUserId) || string.IsNullOrWhiteSpace(actorUserId))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Tenant e identificadores de utilizador são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<RegisterAccountResult>.Fail(404, "Tenant não encontrado ou inativo.");

        var isAdmin = AccountAccessRules.IsAccountsAdmin(actorRoleClaims, actorScopeClaims);
        if (!isAdmin && !string.Equals(actorUserId.Trim(), targetUserId.Trim(), StringComparison.OrdinalIgnoreCase))
            return ServiceResult<RegisterAccountResult>.Fail(403, "Só pode concluir o seu próprio registo ou ser administrador.");

        var existingJson = await userAccountWriter.GetUserDocumentJsonAsync(tenant.Id, targetUserId.Trim(), cancellationToken);
        if (existingJson is null)
            return ServiceResult<RegisterAccountResult>.Fail(404, "Conta não encontrada.");

        JsonNode? existingRoot;
        try
        {
            existingRoot = JsonNode.Parse(existingJson);
        }
        catch
        {
            return ServiceResult<RegisterAccountResult>.Fail(400, "Documento de utilizador inválido.");
        }

        if (existingRoot is not JsonObject userDoc)
            return ServiceResult<RegisterAccountResult>.Fail(400, "Documento de utilizador inválido.");

        AccountSecurityReservedFields.RemoveFrom(userDoc);

        var currentStatus = userDoc["status"] is JsonValue statusNode && statusNode.TryGetValue<string>(out var statusValue)
            ? statusValue
            : AccountStatuses.Active;

        if (currentStatus == AccountStatuses.Active)
            return ServiceResult<RegisterAccountResult>.Fail(409, "O registo já está concluído.");

        if (currentStatus == AccountStatuses.PendingConfirmation)
            return ServiceResult<RegisterAccountResult>.Fail(409, "Confirme o email para concluir o registo.");

        if (currentStatus != AccountStatuses.Incomplete)
            return ServiceResult<RegisterAccountResult>.Fail(400, "A conta não está em estado de registo incompleto.");

        var schemaEntity = await AccountRegistrationRequestHelper.ResolveSchemaForUserAsync(
            schemaRepository,
            tenant.Id,
            userDoc,
            cancellationToken);
        if (schemaEntity is null)
            return ServiceResult<RegisterAccountResult>.Fail(400, "Schema de conta não encontrado para este utilizador.");

        var schemaId = userDoc[AccountUserFields.SchemaId] is JsonValue schemaIdNode && schemaIdNode.TryGetValue<string>(out var sid)
            ? sid
            : schemaEntity.Key;

        var validationJson = userDoc.ToJsonString();
        var errors = jsonSchemaValidation.Validate(schemaEntity.SchemaJson, validationJson);
        if (errors.Count > 0)
            return ServiceResult<RegisterAccountResult>.Fail(400, errors.ToArray());

        if (userDoc["email"] is not JsonValue emailNode || !emailNode.TryGetValue<string>(out var email) || string.IsNullOrWhiteSpace(email))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo email em falta ou inválido.");

        if (userDoc["username"] is not JsonValue userNode || !userNode.TryGetValue<string>(out var username) || string.IsNullOrWhiteSpace(username))
            return ServiceResult<RegisterAccountResult>.Fail(400, "Campo username em falta ou inválido.");

        email = email.Trim().ToLowerInvariant();
        username = username.Trim().ToLowerInvariant();
        userDoc["email"] = email;
        userDoc["username"] = username;

        var config = schemaEntity.GetConfig();
        var confirmationReturnUrl = request?.ConfirmationReturnUrl?.Trim();

        if (AccountSchemaConfigParser.TryGetRegistrationExpiry(config, out var daysExpiry))
            userDoc["registrationExpiresAtUtc"] = JsonValue.Create(DateTime.UtcNow.AddDays(daysExpiry));

        var twoFactorResult = await twoFactorSupport.ApplyAsync(
            userDoc,
            config,
            tenant.Id,
            email,
            username,
            confirmationReturnUrl,
            cancellationToken);

        await userAccountWriter.ReplaceUserDocumentAsync(tenant.Id, targetUserId.Trim(), userDoc.ToJsonString(), cancellationToken);

        var message = twoFactorResult.RequiresEmailConfirmation
            ? "Registo concluído. Confirme o email para ativar."
            : "Registo concluído com sucesso.";

        return ServiceResult<RegisterAccountResult>.Ok(
            new RegisterAccountResult(
                targetUserId.Trim(),
                schemaId,
                twoFactorResult.RequiresEmailConfirmation,
                twoFactorResult.ConfirmationUrl),
            200,
            message);
    }
}
