using Lightcode.Registration.Application.Contracts.Expiry;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAccountExpiryNotificationSender
{
    Task SendExpiryReminderAsync(RegistrationExpiryReminderMessage message, CancellationToken cancellationToken = default);
}
