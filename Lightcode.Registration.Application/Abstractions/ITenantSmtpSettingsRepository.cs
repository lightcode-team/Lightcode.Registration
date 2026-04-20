using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITenantSmtpSettingsRepository
{
    Task<TenantSmtpConfiguration?> GetSmtpAsync(string tenantId, CancellationToken cancellationToken = default);
}
