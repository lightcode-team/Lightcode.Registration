namespace Lightcode.Registration.Application.Accounts;

public static class AccountEmailConfirmationFields
{
    public const string SecretHash = "emailConfirmationSecretHash";
    public const string ExpiresAtUtc = "emailConfirmationExpiresAtUtc";

    public const string CodeTemplateKey = "email-confirmation-code";
    public const string LinkTemplateKey = "email-confirmation-link";

    public const int ExpirationMinutes = 30;
}
