using Lightcode.Registration.Application.Common;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountEmailConfirmationAppService
{
    Task<ServiceResult<object>> ConfirmByCodeAsync(
        string tenantId,
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<object>> ConfirmByLinkAsync(
        string tenantId,
        string email,
        string token,
        CancellationToken cancellationToken = default);
}
