using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountAdminAppService
{
    Task<ServiceResult<RegisterAccountResult>> RegisterByAdminAsync(
        string tenantId,
        string requestJson,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UpdateAccountRolesResult>> UpdateRolesAsync(
        string tenantId,
        string userId,
        UpdateAccountRolesRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<UserAccountListItemDto>>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<UserAccountDetailDto>> GetByIdAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default);
}
