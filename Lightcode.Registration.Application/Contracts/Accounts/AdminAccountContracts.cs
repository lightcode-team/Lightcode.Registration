namespace Lightcode.Registration.Application.Contracts.Accounts;

public sealed record UpdateAccountRolesRequest(IReadOnlyList<string> Roles);

public sealed record UpdateAccountRolesResult(IReadOnlyList<string> Roles);
