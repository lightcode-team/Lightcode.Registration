using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITwoFactorChallengeService
{
    Task<TwoFactorChallengeDto> CreateEmailChallengeAsync(
        TwoFactorChallengeSubject subject,
        string purpose,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<TwoFactorChallenge>> VerifyAsync(
        string challengeId,
        string code,
        string? tenantId,
        string subjectType,
        string purpose,
        CancellationToken cancellationToken = default);
}
