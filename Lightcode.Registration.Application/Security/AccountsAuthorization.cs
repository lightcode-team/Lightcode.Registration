namespace Lightcode.Registration.Application.Security;

public static class AccountsPolicyNames
{
    public const string AccountsAdmin = "AccountsAdmin";
}

public static class AccountAccessRules
{
    public static bool IsAccountsAdmin(IEnumerable<string>? roleClaims, IEnumerable<string>? scopeClaims) =>
        UserRoles.IsAdminFromClaims(roleClaims)
        || (scopeClaims?.Any(s => string.Equals(s, OAuthClientsScopes.Owner, StringComparison.OrdinalIgnoreCase)) ?? false);
}
