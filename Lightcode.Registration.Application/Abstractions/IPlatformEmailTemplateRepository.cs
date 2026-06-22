using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IPlatformEmailTemplateRepository
{
    Task<EmailTemplate?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<EmailTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task InsertIfMissingAsync(EmailTemplate template, CancellationToken cancellationToken = default);
}
