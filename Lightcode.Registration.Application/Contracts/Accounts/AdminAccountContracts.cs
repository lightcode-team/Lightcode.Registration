namespace Lightcode.Registration.Application.Contracts.Accounts;

public sealed record AdminRegisterAccountRequest(
    string Email,
    string Username,
    string Password,
    IReadOnlyList<string>? Roles);

public sealed record UpdateAccountRolesRequest(IReadOnlyList<string> Roles);

public sealed record UpdateAccountRolesResult(IReadOnlyList<string> Roles);
