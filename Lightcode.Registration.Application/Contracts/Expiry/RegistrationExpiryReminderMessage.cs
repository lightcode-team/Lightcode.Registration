namespace Lightcode.Registration.Application.Contracts.Expiry;

public sealed record RegistrationExpiryReminderMessage(
    string TenantId,
    string UserId,
    string Email,
    /// <summary>30 ou 15.</summary>
    int ReminderKind,
    DateTime RegistrationExpiresAtUtc);
