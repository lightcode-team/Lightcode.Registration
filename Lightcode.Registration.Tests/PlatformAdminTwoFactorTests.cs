using System.Security.Claims;
using FluentAssertions;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Contracts.Platform;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.Services;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class PlatformAdminTwoFactorTests
{
    [Fact]
    public async Task Platform_admin_login_without_two_factor_emits_token()
    {
        var ctx = TestContext.Create(twoFactorEnabled: false);

        var result = await ctx.Service.IssueTokenAsync(new PlatformAdminTokenRequest(ctx.Admin.Email, "password"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeFalse();
        result.Value.Token!.AccessToken.Should().Be("platform-admin-1");
        ctx.ChallengeService.Created.Should().BeFalse();
    }

    [Fact]
    public async Task Platform_admin_login_with_two_factor_returns_challenge_without_token()
    {
        var ctx = TestContext.Create(twoFactorEnabled: true);

        var result = await ctx.Service.IssueTokenAsync(new PlatformAdminTokenRequest(ctx.Admin.Email, "password"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeTrue();
        result.Value.Token.Should().BeNull();
        result.Value.Challenge!.ChallengeId.Should().Be("challenge-platform");
        ctx.ChallengeService.Created.Should().BeTrue();
    }

    [Fact]
    public async Task Platform_admin_confirm_two_factor_emits_platform_token_with_mfa_claims()
    {
        var ctx = TestContext.Create(twoFactorEnabled: true);
        ctx.ChallengeService.VerificationResult = ServiceResult<TwoFactorChallenge>.Ok(new TwoFactorChallenge
        {
            Id = "challenge-platform",
            SubjectType = TwoFactorSubjectTypes.PlatformAdmin,
            SubjectId = ctx.Admin.Id,
            Purpose = TwoFactorChallengePurposes.Login,
            Method = TwoFactorMethods.EmailCode,
            DestinationHint = "a***@example.com",
            Status = TwoFactorChallengeStatuses.Used,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            CreatedAtUtc = DateTime.UtcNow
        });

        var result = await ctx.Service.ConfirmTwoFactorAsync(new ConfirmTwoFactorRequest("challenge-platform", "123456"));

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeFalse();
        result.Value.Token!.AccessToken.Should().Be("platform-admin-1");
        ctx.AccessTokenIssuer.PlatformClaims.Select(x => x.Type).Should().Contain(["amr", "auth_time", "mfa_method"]);
    }

    private sealed class TestContext
    {
        private TestContext(bool twoFactorEnabled)
        {
            Repository = new FakePlatformAdminRepository(Admin);
            SettingsService = new FakeTwoFactorSettingsService(twoFactorEnabled);
            ChallengeService = new FakeTwoFactorChallengeService(Admin);
            AccessTokenIssuer = new FakeAccessTokenIssuer();

            Service = new PlatformAdminAppService(
                Repository,
                new FakeTenantLookup(),
                new FakePasswordHasher(),
                new FakeSecureTokenGenerator(),
                AccessTokenIssuer,
                new FakeTenantSigningKeyResolver(),
                new FakeEmailEnqueuePublisher(),
                SettingsService,
                ChallengeService,
                Options.Create(new MasterOptions()),
                Options.Create(new JwtOptions { SigningKey = "12345678901234567890123456789012" }),
                Options.Create(new RegistrationOptions()),
                new FakeRuntimeEnvironment());
        }

        public PlatformAdmin Admin { get; } = new()
        {
            Id = "admin-1",
            Email = "admin@example.com",
            PasswordHash = "password",
            Status = PlatformAdminStatuses.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        public FakePlatformAdminRepository Repository { get; }
        public FakeTwoFactorSettingsService SettingsService { get; }
        public FakeTwoFactorChallengeService ChallengeService { get; }
        public FakeAccessTokenIssuer AccessTokenIssuer { get; }
        public PlatformAdminAppService Service { get; }

        public static TestContext Create(bool twoFactorEnabled) => new(twoFactorEnabled);
    }

    private sealed class FakePlatformAdminRepository(PlatformAdmin admin) : IPlatformAdminRepository
    {
        public Task<PlatformAdmin?> FindAdminByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<PlatformAdmin?>(string.Equals(email, admin.Email, StringComparison.Ordinal) ? admin : null);

        public Task<PlatformAdmin?> GetAdminByIdAsync(string adminId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PlatformAdmin?>(string.Equals(adminId, admin.Id, StringComparison.Ordinal) ? admin : null);

        public Task InsertAdminAsync(PlatformAdmin admin, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceAdminAsync(PlatformAdmin admin, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InsertInviteAsync(PlatformAdminInvite invite, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PlatformAdminInvite?> FindPendingInviteByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => Task.FromResult<PlatformAdminInvite?>(null);
        public Task MarkInviteUsedAsync(string inviteId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertTenantLinkAsync(string adminId, string tenantId, string role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PlatformAdminTenantLink?> FindActiveTenantLinkAsync(string adminId, string tenantId, CancellationToken cancellationToken = default) => Task.FromResult<PlatformAdminTenantLink?>(null);
        public Task<IReadOnlyList<PlatformTenantDto>> ListActiveTenantsForAdminAsync(string adminId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PlatformTenantDto>>([]);
    }

    private sealed class FakeTwoFactorSettingsService(bool enabled) : ITwoFactorSettingsService
    {
        public Task<UserTwoFactorSettings> GetUserSettingsAsync(string tenantId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(new UserTwoFactorSettings(false, TwoFactorMethods.EmailCode, false));
        public Task SetUserEmailTwoFactorAsync(string tenantId, string userId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UserTwoFactorSettings> GetPlatformAdminSettingsAsync(string adminId, CancellationToken cancellationToken = default) => Task.FromResult(new UserTwoFactorSettings(enabled, TwoFactorMethods.EmailCode, enabled));
        public Task SetPlatformAdminEmailTwoFactorAsync(string adminId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTwoFactorChallengeService(PlatformAdmin admin) : ITwoFactorChallengeService
    {
        public bool Created { get; private set; }
        public ServiceResult<TwoFactorChallenge> VerificationResult { get; set; } = ServiceResult<TwoFactorChallenge>.Fail(401, "invalid");

        public Task<TwoFactorChallengeDto> CreateEmailChallengeAsync(TwoFactorChallengeSubject subject, string purpose, CancellationToken cancellationToken = default)
        {
            subject.SubjectType.Should().Be(TwoFactorSubjectTypes.PlatformAdmin);
            subject.SubjectId.Should().Be(admin.Id);
            subject.TenantId.Should().BeNull();
            Created = true;
            return Task.FromResult(new TwoFactorChallengeDto("challenge-platform", TwoFactorMethods.EmailCode, 300, "a***@example.com"));
        }

        public Task<ServiceResult<TwoFactorChallenge>> VerifyAsync(string challengeId, string code, string? tenantId, string subjectType, string purpose, CancellationToken cancellationToken = default)
        {
            tenantId.Should().BeNull();
            subjectType.Should().Be(TwoFactorSubjectTypes.PlatformAdmin);
            purpose.Should().Be(TwoFactorChallengePurposes.Login);
            return Task.FromResult(VerificationResult);
        }
    }

    private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
    {
        public IReadOnlyList<Claim> PlatformClaims { get; private set; } = [];

        public IssueTokenResponse CreateAccessToken(string subjectId, string tenantId, TokenIssuanceProfile profile, TenantSigningKeyMaterial signingKey, IEnumerable<Claim>? additionalClaims = null) =>
            new($"access-{subjectId}", "Bearer", 3600);

        public IssueTokenResponse CreatePlatformAdminAccessToken(string adminId, string email, IEnumerable<Claim>? additionalClaims = null)
        {
            PlatformClaims = additionalClaims?.ToList() ?? [];
            return new($"platform-{adminId}", "Bearer", 3600);
        }
    }

    private sealed class FakeTenantLookup : ITenantLookup
    {
        public Task<Tenant?> FindActiveByIdAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> ListActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plainTextPassword) => plainTextPassword;
        public bool Verify(string plainTextPassword, string storedHash) => plainTextPassword == storedHash;
    }

    private sealed class FakeSecureTokenGenerator : ISecureTokenGenerator
    {
        public string GenerateEmailConfirmationCode() => "123456";
        public string GenerateEmailConfirmationToken() => "email-token";
        public string GeneratePasswordResetToken() => "reset-token";
        public string GenerateRefreshToken() => "refresh-token";
        public string GenerateClientSecret() => "client-secret";
    }

    private sealed class FakeTenantSigningKeyResolver : ITenantSigningKeyResolver
    {
        public Task<TenantSigningKeyMaterial> ResolveSigningKeyAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantSigningKeyMaterial("", "", "", 1));

        public string? ResolvePublicKeyJwk(string tenantId) => null;
    }

    private sealed class FakeEmailEnqueuePublisher : IEmailEnqueuePublisher
    {
        public Task<string> PublishSendAsync(EmailDispatchQueueMessage message, CancellationToken cancellationToken = default) => Task.FromResult("message-id");
    }

    private sealed class FakeRuntimeEnvironment : IRuntimeEnvironment
    {
        public bool IsDevelopment => true;
    }
}
