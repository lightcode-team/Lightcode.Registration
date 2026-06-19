using System.Security.Cryptography;
using System.Text;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.TwoFactor;

public sealed class TwoFactorChallengeService(
    ITwoFactorChallengeRepository repository,
    ITwoFactorMethodProvider methodProvider,
    ISecureTokenGenerator tokenGenerator,
    IOptions<JwtOptions> jwtOptions) : ITwoFactorChallengeService
{
    private const int ExpirationMinutes = 5;
    private const int MaxAttempts = 5;

    public async Task<TwoFactorChallengeDto> CreateEmailChallengeAsync(
        TwoFactorChallengeSubject subject,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var challengeId = Guid.NewGuid().ToString("N");
        var code = tokenGenerator.GenerateEmailConfirmationCode();

        await repository.InvalidatePendingAsync(
            subject.TenantId,
            subject.SubjectType,
            subject.SubjectId,
            purpose,
            TwoFactorMethods.EmailCode,
            cancellationToken);

        var challenge = new TwoFactorChallenge
        {
            Id = challengeId,
            SubjectType = subject.SubjectType,
            SubjectId = subject.SubjectId,
            TenantId = subject.TenantId,
            Purpose = purpose,
            Method = TwoFactorMethods.EmailCode,
            DestinationHint = MaskEmail(subject.Email),
            CodeHash = HashCode(challengeId, subject.SubjectId, purpose, code),
            Status = TwoFactorChallengeStatuses.Pending,
            Attempts = 0,
            MaxAttempts = MaxAttempts,
            ExpiresAtUtc = now.AddMinutes(ExpirationMinutes),
            CreatedAtUtc = now
        };

        await repository.InsertAsync(challenge, cancellationToken);
        await methodProvider.GetRequired(TwoFactorMethods.EmailCode)
            .SendAsync(subject, code, purpose, cancellationToken);

        return new TwoFactorChallengeDto(
            challenge.Id,
            challenge.Method,
            ExpirationMinutes * 60,
            challenge.DestinationHint);
    }

    public async Task<ServiceResult<TwoFactorChallenge>> VerifyAsync(
        string challengeId,
        string code,
        string? tenantId,
        string subjectType,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(code))
            return ServiceResult<TwoFactorChallenge>.Fail(400, "Challenge e código são obrigatórios.");

        var challenge = await repository.FindAsync(
            challengeId.Trim(),
            tenantId,
            subjectType,
            purpose,
            cancellationToken);

        if (challenge is null
            || challenge.Status != TwoFactorChallengeStatuses.Pending
            || challenge.ExpiresAtUtc <= DateTime.UtcNow
            || challenge.Attempts >= challenge.MaxAttempts)
            return ServiceResult<TwoFactorChallenge>.Fail(401, "Código 2FA inválido ou expirado.");

        var expected = HashCode(challenge.Id, challenge.SubjectId, challenge.Purpose, code.Trim());
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(challenge.CodeHash)))
        {
            await repository.TryIncrementAttemptsAsync(challenge.Id, tenantId, challenge.MaxAttempts, cancellationToken);
            return ServiceResult<TwoFactorChallenge>.Fail(401, "Código 2FA inválido ou expirado.");
        }

        var used = await repository.TryMarkUsedAsync(challenge.Id, tenantId, cancellationToken);
        if (!used)
            return ServiceResult<TwoFactorChallenge>.Fail(409, "Challenge 2FA já utilizado.");

        return ServiceResult<TwoFactorChallenge>.Ok(challenge);
    }

    private string HashCode(string challengeId, string subjectId, string purpose, string code)
    {
        var key = Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey);
        var data = Encoding.UTF8.GetBytes($"{challengeId}:{subjectId}:{purpose}:{code}");
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
            return "***";

        return $"{email[0]}***{email[at..]}";
    }
}
