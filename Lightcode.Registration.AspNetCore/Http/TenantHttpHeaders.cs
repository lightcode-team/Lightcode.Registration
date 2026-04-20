using Microsoft.AspNetCore.Http;

namespace Lightcode.Registration.Http;

/// <summary>Cabeçalhos HTTP para contexto multi-tenant quando não há JWT (ex.: login).</summary>
public static class TenantHttpHeaders
{
    /// <summary>Identificador do tenant (obrigatório em pedidos anónimos que dependem do tenant).</summary>
    public const string TenantId = "X-Tenant-Id";

    /// <summary>Devolve o tenant do cabeçalho <see cref="TenantId"/> já normalizado, ou <c>null</c> se em falta.</summary>
    public static string? TryGetTenantId(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(TenantId, out var values))
            return null;

        var id = values.FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
