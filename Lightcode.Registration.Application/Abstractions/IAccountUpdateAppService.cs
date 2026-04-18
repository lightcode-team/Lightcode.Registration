using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountUpdateAppService
{
    /// <param name="actorRoleClaims">Valores das claims <c>role</c> do JWT do ator.</param>
    Task<ServiceResult<UpdateAccountResult>> UpdateAsync(
        string tenantId,
        string targetUserId,
        string actorUserId,
        IEnumerable<string> actorRoleClaims,
        string patchJson,
        CancellationToken cancellationToken = default);
}
