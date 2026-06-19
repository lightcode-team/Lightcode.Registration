using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITwoFactorChallengeRepository
{
    Task InsertAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default);

    Task InvalidatePendingAsync(
        string? tenantId,
        string subjectType,
        string subjectId,
        string purpose,
        string method,
        CancellationToken cancellationToken = default);

    Task<TwoFactorChallenge?> FindAsync(
        string challengeId,
        string? tenantId,
        string subjectType,
        string purpose,
        CancellationToken cancellationToken = default);

    Task<bool> TryMarkUsedAsync(string challengeId, string? tenantId, CancellationToken cancellationToken = default);

    Task<bool> TryIncrementAttemptsAsync(
        string challengeId,
        string? tenantId,
        int maxAttempts,
        CancellationToken cancellationToken = default);
}
