using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IEmailTemplateRepository
{
    Task<IReadOnlyList<EmailTemplate>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<EmailTemplate?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task<EmailTemplate?> GetByKeyAsync(string tenantId, string key, CancellationToken cancellationToken = default);

    Task InsertAsync(EmailTemplate entity, CancellationToken cancellationToken = default);

    Task ReplaceAsync(EmailTemplate entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
