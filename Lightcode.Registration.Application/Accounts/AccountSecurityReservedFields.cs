namespace Lightcode.Registration.Application.Accounts;

public static class AccountSecurityReservedFields
{
    public const string TwoFactorSettings = "twoFactorSettings";

    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "twoFactor",
        TwoFactorSettings,
        "mfa",
        "totpSecret",
        "totpSecretEncrypted",
        "recoveryCodes",
        "trustedDevices",
        "twoFactorEnabled"
    };

    public static void RemoveFrom(System.Text.Json.Nodes.JsonObject obj)
    {
        foreach (var field in Names)
            obj.Remove(field);
    }
}
