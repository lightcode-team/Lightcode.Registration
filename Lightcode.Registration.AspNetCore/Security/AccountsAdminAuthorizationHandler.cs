using Lightcode.Registration.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace Lightcode.Registration.AspNetCore.Security;

public sealed class AccountsAdminRequirement : IAuthorizationRequirement;

public sealed class AccountsAdminAuthorizationHandler : AuthorizationHandler<AccountsAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccountsAdminRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(context.User.FindFirst("tenantId")?.Value))
            return Task.CompletedTask;

        var roles = context.User.FindAll("role").Select(c => c.Value);
        var scopes = context.User.FindAll("scope").Select(c => c.Value);

        if (AccountAccessRules.IsAccountsAdmin(roles, scopes))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
