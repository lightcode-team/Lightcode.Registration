using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Lightcode.Registration.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantLookup tenantLookup)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.FindFirst("tenantId")?.Value;
            if (!string.IsNullOrEmpty(tenantId))
            {
                var tenant = await tenantLookup.FindActiveByIdAsync(tenantId, context.RequestAborted);
                if (tenant is null)
                {
                    await ApiResponse.WriteErrorAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        "Tenant inválido ou inativo.");
                    return;
                }

                context.Items["Tenant"] = tenant;
            }
        }

        await next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
