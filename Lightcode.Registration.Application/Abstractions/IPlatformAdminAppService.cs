using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Contracts.Platform;

namespace Lightcode.Registration.Application.Abstractions;

public interface IPlatformAdminAppService
{
    Task<ServiceResult<InvitePlatformAdminResult>> InviteAsync(
        InvitePlatformAdminCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ActivatePlatformAdminResult>> ActivateAsync(
        ActivatePlatformAdminRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthTokenResponse>> IssueTokenAsync(
        PlatformAdminTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthTokenResponse>> ConfirmTwoFactorAsync(
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TwoFactorBeginResponse>> BeginEnableEmailTwoFactorAsync(
        string adminId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ConfirmEnableEmailTwoFactorAsync(
        string adminId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TwoFactorBeginResponse>> BeginDisableTwoFactorAsync(
        string adminId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ConfirmDisableTwoFactorAsync(
        string adminId,
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PlatformAdminTwoFactorStatusResult>> GetTwoFactorStatusAsync(
        string adminId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<PlatformTenantDto>>> ListTenantsAsync(
        string adminId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PlatformTenantTokenResult>> IssueTenantTokenAsync(
        string adminId,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InvitePlatformAdminResult>> EnsureTenantOwnerAsync(
        string email,
        string tenantId,
        bool sendEmail = true,
        CancellationToken cancellationToken = default);
}
