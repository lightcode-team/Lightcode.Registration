using Lightcode.Registration.Application.Common;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountPasswordResetAppService
{
    Task<ServiceResult<object>> ForgotPasswordAsync(
        string tenantId,
        string? email,
        string? username,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object>> ForgotPasswordAsync(
        string tenantId,
        string? email,
        string? username,
        string? continuationId,
        CancellationToken cancellationToken = default) =>
        ForgotPasswordAsync(tenantId, email, username, cancellationToken);

    Task<ServiceResult<object>> ResetPasswordAsync(
        string tenantId,
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}
