using FluentAssertions;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class TwoFactorChallengeServiceTests
{
    [Fact]
    public async Task VerifyAsync_accepts_valid_code_once()
    {
        var repo = new FakeChallengeRepository();
        var method = new FakeTwoFactorMethod();
        var service = CreateService(repo, method);
        var subject = Subject();

        var challenge = await service.CreateEmailChallengeAsync(subject, TwoFactorChallengePurposes.Login);

        var first = await service.VerifyAsync(
            challenge.ChallengeId,
            method.LastCode!,
            subject.TenantId,
            TwoFactorSubjectTypes.TenantUser,
            TwoFactorChallengePurposes.Login);

        var second = await service.VerifyAsync(
            challenge.ChallengeId,
            method.LastCode!,
            subject.TenantId,
            TwoFactorSubjectTypes.TenantUser,
            TwoFactorChallengePurposes.Login);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeFalse();
        second.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task VerifyAsync_increments_attempts_for_invalid_code()
    {
        var repo = new FakeChallengeRepository();
        var method = new FakeTwoFactorMethod();
        var service = CreateService(repo, method);
        var subject = Subject();

        var challenge = await service.CreateEmailChallengeAsync(subject, TwoFactorChallengePurposes.Login);

        var result = await service.VerifyAsync(
            challenge.ChallengeId,
            "000000",
            subject.TenantId,
            TwoFactorSubjectTypes.TenantUser,
            TwoFactorChallengePurposes.Login);

        result.IsSuccess.Should().BeFalse();
        repo.Items[challenge.ChallengeId].Attempts.Should().Be(1);
    }

    private static TwoFactorChallengeService CreateService(
        FakeChallengeRepository repo,
        FakeTwoFactorMethod method)
    {
        var provider = new TwoFactorMethodProvider([method]);
        return new TwoFactorChallengeService(
            repo,
            provider,
            new FakeTokenGenerator(),
            Options.Create(new JwtOptions { SigningKey = "12345678901234567890123456789012" }));
    }

    private static TwoFactorChallengeSubject Subject() =>
        new(TwoFactorSubjectTypes.TenantUser, "user-1", "tenant-1", "user@example.com", "user@example.com");

    private sealed class FakeTokenGenerator : ISecureTokenGenerator
    {
        public string GenerateRefreshToken() => "refresh";
        public string GenerateClientSecret() => "secret";
        public string GenerateEmailConfirmationCode() => "123456";
        public string GenerateEmailConfirmationToken() => "token";
        public string GeneratePasswordResetToken() => "reset";
    }

    private sealed class FakeTwoFactorMethod : ITwoFactorMethod
    {
        public string? LastCode { get; private set; }
        public string Method => TwoFactorMethods.EmailCode;

        public Task SendAsync(
            TwoFactorChallengeSubject subject,
            string code,
            string purpose,
            CancellationToken cancellationToken = default)
        {
            LastCode = code;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChallengeRepository : ITwoFactorChallengeRepository
    {
        public Dictionary<string, TwoFactorChallenge> Items { get; } = new(StringComparer.Ordinal);

        public Task InsertAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default)
        {
            Items[challenge.Id] = challenge;
            return Task.CompletedTask;
        }

        public Task InvalidatePendingAsync(
            string? tenantId,
            string subjectType,
            string subjectId,
            string purpose,
            string method,
            CancellationToken cancellationToken = default)
        {
            foreach (var item in Items.Values.Where(x =>
                         x.TenantId == tenantId
                         && x.SubjectType == subjectType
                         && x.SubjectId == subjectId
                         && x.Purpose == purpose
                         && x.Method == method
                         && x.Status == TwoFactorChallengeStatuses.Pending))
                item.Status = TwoFactorChallengeStatuses.Expired;

            return Task.CompletedTask;
        }

        public Task<TwoFactorChallenge?> FindAsync(
            string challengeId,
            string? tenantId,
            string subjectType,
            string purpose,
            CancellationToken cancellationToken = default)
        {
            Items.TryGetValue(challengeId, out var challenge);
            if (challenge is null
                || challenge.TenantId != tenantId
                || challenge.SubjectType != subjectType
                || challenge.Purpose != purpose)
                return Task.FromResult<TwoFactorChallenge?>(null);

            return Task.FromResult<TwoFactorChallenge?>(challenge);
        }

        public Task<bool> TryMarkUsedAsync(
            string challengeId,
            string? tenantId,
            CancellationToken cancellationToken = default)
        {
            if (!Items.TryGetValue(challengeId, out var challenge)
                || challenge.TenantId != tenantId
                || challenge.Status != TwoFactorChallengeStatuses.Pending
                || challenge.ExpiresAtUtc <= DateTime.UtcNow
                || challenge.Attempts >= challenge.MaxAttempts)
                return Task.FromResult(false);

            challenge.Status = TwoFactorChallengeStatuses.Used;
            challenge.ConsumedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }

        public Task<bool> TryIncrementAttemptsAsync(
            string challengeId,
            string? tenantId,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            if (!Items.TryGetValue(challengeId, out var challenge)
                || challenge.TenantId != tenantId
                || challenge.Attempts >= maxAttempts)
                return Task.FromResult(false);

            challenge.Attempts++;
            if (challenge.Attempts >= maxAttempts)
                challenge.Status = TwoFactorChallengeStatuses.Failed;

            return Task.FromResult(true);
        }
    }
}
