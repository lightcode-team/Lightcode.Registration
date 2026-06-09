using Lightcode.Registration.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace Lightcode.Registration.AspNetCore.Security;

public sealed class OAuthClientsPermissionRequirement(OAuthClientsPermission permission) : IAuthorizationRequirement
{
    public OAuthClientsPermission Permission { get; } = permission;
}

public sealed class OAuthClientsPermissionAuthorizationHandler
    : AuthorizationHandler<OAuthClientsPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OAuthClientsPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(context.User.FindFirst("tenantId")?.Value))
            return Task.CompletedTask;

        if (HasScope(context.User, OAuthClientsScopes.Owner))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var requiredRole = requirement.Permission switch
        {
            OAuthClientsPermission.ClientsRead => OAuthClientsRoles.ClientsRead,
            OAuthClientsPermission.ClientsWrite => OAuthClientsRoles.ClientsWrite,
            _ => null
        };

        if (requiredRole is not null && context.User.IsInRole(requiredRole))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope) =>
        user.FindAll("scope")
            .Any(c => string.Equals(c.Value, scope, StringComparison.OrdinalIgnoreCase));
}
