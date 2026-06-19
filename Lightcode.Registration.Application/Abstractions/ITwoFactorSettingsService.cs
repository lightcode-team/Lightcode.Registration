using Lightcode.Registration.Application.TwoFactor;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITwoFactorSettingsService
{
    Task<UserTwoFactorSettings> GetUserSettingsAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default);

    Task SetUserEmailTwoFactorAsync(
        string tenantId,
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default);

    Task<UserTwoFactorSettings> GetPlatformAdminSettingsAsync(
        string adminId,
        CancellationToken cancellationToken = default);

    Task SetPlatformAdminEmailTwoFactorAsync(
        string adminId,
        bool enabled,
        CancellationToken cancellationToken = default);
}
