using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountCompleteRegistrationAppService
{
    Task<ServiceResult<RegisterAccountResult>> CompleteRegisterAsync(
        string tenantId,
        string targetUserId,
        string actorUserId,
        IEnumerable<string> actorRoleClaims,
        IEnumerable<string> actorScopeClaims,
        CompleteRegisterRequest? request,
        CancellationToken cancellationToken = default);
}
