using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountTwoFactorAppService
{
    Task<ServiceResult<TwoFactorBeginResponse>> BeginEnableEmailAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object>> ConfirmEnableEmailAsync(
        string tenantId,
        string userId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TwoFactorBeginResponse>> BeginDisableAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object>> ConfirmDisableAsync(
        string tenantId,
        string userId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default);
}
