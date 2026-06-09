using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Accounts;

public sealed record RegistrationTwoFactorApplyResult(
    bool RequiresEmailConfirmation,
    string? ConfirmationUrl);

public sealed class AccountRegistrationTwoFactorSupport(
    ISecureTokenGenerator tokenGenerator,
    IPasswordHasher passwordHasher,
    IEmailEnqueuePublisher emailEnqueuePublisher,
    IOptions<RegistrationOptions> registrationOptions)
{
    public bool IsTwoFactorActive(AccountJsonSchemaConfig config) =>
        config.TwoFactor is { Active: true } && config.TwoFactor.Type.HasValue;

    public async Task<RegistrationTwoFactorApplyResult> ApplyAsync(
        JsonObject userDoc,
        AccountJsonSchemaConfig config,
        string tenantId,
        string email,
        string username,
        string? confirmationReturnUrl,
        CancellationToken cancellationToken)
    {
        if (!IsTwoFactorActive(config))
        {
            userDoc["status"] = JsonValue.Create(AccountStatuses.Active);
            return new RegistrationTwoFactorApplyResult(false, null);
        }

        var twoFactorType = config.TwoFactor!.Type!.Value;
        var plainSecret = twoFactorType == EmailTwoFactorType.Code
            ? tokenGenerator.GenerateEmailConfirmationCode()
            : tokenGenerator.GenerateEmailConfirmationToken();

        var expiresAt = DateTime.UtcNow.AddMinutes(AccountEmailConfirmationFields.ExpirationMinutes);
        userDoc["status"] = JsonValue.Create(AccountStatuses.PendingConfirmation);
        userDoc[AccountEmailConfirmationFields.SecretHash] = JsonValue.Create(passwordHasher.Hash(plainSecret));
        userDoc[AccountEmailConfirmationFields.ExpiresAtUtc] = JsonValue.Create(expiresAt);

        string? confirmationUrl = null;
        if (twoFactorType == EmailTwoFactorType.Code
            && !string.IsNullOrWhiteSpace(confirmationReturnUrl))
        {
            confirmationUrl = BuildConfirmationReturnUrl(confirmationReturnUrl.Trim(), tenantId, email);
        }

        var templateKey = twoFactorType == EmailTwoFactorType.Code
            ? AccountEmailConfirmationFields.CodeTemplateKey
            : AccountEmailConfirmationFields.LinkTemplateKey;

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["username"] = username
        };

        if (twoFactorType == EmailTwoFactorType.Code)
            parameters["code"] = plainSecret;
        else
            parameters["confirmationLink"] = BuildApiConfirmationLink(tenantId, email, plainSecret);

        await emailEnqueuePublisher.PublishSendAsync(
            new EmailDispatchQueueMessage(
                tenantId,
                TemplateId: null,
                TemplateKey: templateKey,
                To: email,
                Parameters: parameters),
            cancellationToken);

        return new RegistrationTwoFactorApplyResult(true, confirmationUrl);
    }

    private string BuildApiConfirmationLink(string tenantId, string email, string token)
    {
        var baseUrl = registrationOptions.Value.PublicApiBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://localhost:5000";

        var encodedTenantId = Uri.EscapeDataString(tenantId);
        var encodedEmail = Uri.EscapeDataString(email);
        return $"{baseUrl}/api/accounts/confirm-email/{token}?tenantId={encodedTenantId}&email={encodedEmail}";
    }

    private static string BuildConfirmationReturnUrl(string returnUrl, string tenantId, string email)
    {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{returnUrl}{separator}tenantId={Uri.EscapeDataString(tenantId)}&email={Uri.EscapeDataString(email)}&code=";
    }
}
