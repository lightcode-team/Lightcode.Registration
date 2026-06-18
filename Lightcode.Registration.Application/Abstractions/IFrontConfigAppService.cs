using Lightcode.Registration.Application.Contracts.Frontend;

namespace Lightcode.Registration.Application.Abstractions;

public interface IFrontConfigAppService
{
    Task<FrontConfigDto> ResolveAsync(string? tenantId, CancellationToken cancellationToken = default);
}
