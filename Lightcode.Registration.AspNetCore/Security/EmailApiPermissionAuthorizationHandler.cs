using Lightcode.Registration.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace Lightcode.Registration.AspNetCore.Security;

public sealed class EmailApiPermissionRequirement(EmailApiPermission permission) : IAuthorizationRequirement
{
    public EmailApiPermission Permission { get; } = permission;
}

public sealed class EmailApiPermissionAuthorizationHandler : AuthorizationHandler<EmailApiPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmailApiPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(context.User.FindFirst("tenantId")?.Value))
            return Task.CompletedTask;

        if (HasScope(context.User, EmailApiScopes.EmailAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var requiredRole = requirement.Permission switch
        {
            EmailApiPermission.TemplateRead => EmailApiRoles.TemplateRead,
            EmailApiPermission.TemplateWrite => EmailApiRoles.TemplateWrite,
            EmailApiPermission.SendEmail => EmailApiRoles.SendEmail,
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
