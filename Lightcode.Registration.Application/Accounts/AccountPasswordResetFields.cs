namespace Lightcode.Registration.Application.Accounts;

public static class AccountPasswordResetFields
{
    public const string TokenHash = "passwordResetTokenHash";
    public const string ExpiresAtUtc = "passwordResetExpiresAtUtc";

    public const string TemplateKey = "password-reset-link";

    public const int ExpirationMinutes = 60;
}
