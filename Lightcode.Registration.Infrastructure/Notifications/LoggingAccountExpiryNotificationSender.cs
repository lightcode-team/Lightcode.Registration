using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Expiry;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.Infrastructure.Notifications;

public sealed class LoggingAccountExpiryNotificationSender(ILogger<LoggingAccountExpiryNotificationSender> logger)
    : IAccountExpiryNotificationSender
{
    public Task SendExpiryReminderAsync(RegistrationExpiryReminderMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Lembrete de expiração de cadastro ({ReminderKind} dias): tenant={TenantId} user={UserId} email={Email} expira em {ExpiresAt:O}",
            message.ReminderKind,
            message.TenantId,
            message.UserId,
            message.Email,
            message.RegistrationExpiresAtUtc);
        return Task.CompletedTask;
    }
}
