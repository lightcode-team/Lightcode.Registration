using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task<(string PlainToken, RefreshToken Entity)> CreateAsync(
        string tenantId,
        string subjectId,
        string subjectType,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> scopes,
        DateTime expiresAtUtc,
        int maxUses,
        CancellationToken cancellationToken = default);

    Task<RefreshToken?> FindActiveByPlainTokenAsync(
        string tenantId,
        string plainToken,
        CancellationToken cancellationToken = default);

    Task<bool> TryIncrementUseCountAsync(string tenantId, string id, CancellationToken cancellationToken = default);

    Task RevokeBySubjectAsync(
        string tenantId,
        string subjectId,
        string subjectType,
        CancellationToken cancellationToken = default);
}
