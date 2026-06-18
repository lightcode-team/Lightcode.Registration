using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IFrontConfigRepository
{
    Task<FrontConfig?> GetActiveAsync(string tenantId, CancellationToken cancellationToken = default);
}
