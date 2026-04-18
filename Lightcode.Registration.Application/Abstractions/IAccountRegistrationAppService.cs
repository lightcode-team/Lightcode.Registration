using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Accounts;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountRegistrationAppService
{
    Task<ServiceResult<RegisterAccountResult>> RegisterAsync(string tenantId, string requestJson, CancellationToken cancellationToken = default);
}
