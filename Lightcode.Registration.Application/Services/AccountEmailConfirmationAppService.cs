using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;

namespace Lightcode.Registration.Application.Services;

public sealed class AccountEmailConfirmationAppService(
    ITenantLookup tenantLookup,
    IUserAccountWriter userAccountWriter) : IAccountEmailConfirmationAppService
{
    public async Task<ServiceResult<object>> ConfirmByCodeAsync(
        string tenantId,
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<object>.Fail(400, "TenantId é obrigatório.");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return ServiceResult<object>.Fail(400, "Email e código são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<object>.Fail(404, "Tenant não encontrado ou inativo.");

        var confirmed = await userAccountWriter.TryConfirmEmailAsync(
            tenant.Id,
            email.Trim().ToLowerInvariant(),
            code.Trim(),
            cancellationToken);

        if (!confirmed)
            return ServiceResult<object>.Fail(400, "Código inválido, expirado ou conta já confirmada.");

        return ServiceResult<object>.Ok(new { }, 200, "Email confirmado com sucesso.");
    }

    public async Task<ServiceResult<object>> ConfirmByLinkAsync(
        string tenantId,
        string email,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<object>.Fail(400, "TenantId é obrigatório.");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return ServiceResult<object>.Fail(400, "Email e token são obrigatórios.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<object>.Fail(404, "Tenant não encontrado ou inativo.");

        var confirmed = await userAccountWriter.TryConfirmEmailAsync(
            tenant.Id,
            email.Trim().ToLowerInvariant(),
            token.Trim(),
            cancellationToken);

        if (!confirmed)
            return ServiceResult<object>.Fail(400, "Link inválido, expirado ou conta já confirmada.");

        return ServiceResult<object>.Ok(new { }, 200, "Email confirmado com sucesso.");
    }
}
