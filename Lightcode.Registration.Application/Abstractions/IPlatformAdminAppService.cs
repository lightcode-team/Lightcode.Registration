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

    Task<ServiceResult<IssueTokenResponse>> IssueTokenAsync(
        PlatformAdminTokenRequest request,
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
        CancellationToken cancellationToken = default);
}
