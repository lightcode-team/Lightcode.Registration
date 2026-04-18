using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.JsonSchema;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountJsonSchemaAppService
{
    Task<ServiceResult<IReadOnlyList<AccountJsonSchemaDto>>> ListAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<ServiceResult<AccountJsonSchemaDto>> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<ServiceResult<AccountJsonSchemaDto>> CreateAsync(string tenantId, CreateAccountJsonSchemaRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<AccountJsonSchemaDto>> UpdateAsync(string tenantId, string id, UpdateAccountJsonSchemaRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<object?>> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
