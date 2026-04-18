using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAuthenticationAppService
{
    Task<ServiceResult<IssueTokenResponse>> IssueTokenAsync(IssueTokenRequest request, CancellationToken cancellationToken = default);
}
