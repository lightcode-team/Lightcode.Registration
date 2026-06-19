using System.Security.Claims;
using FluentAssertions;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.Services;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class AuthenticationAppServiceTests
{
    [Fact]
    public async Task Client_credentials_emits_token_without_two_factor_challenge()
    {
        var ctx = TestContext.Create();
        ctx.OAuthClient = new OAuthClient
        {
            ClientId = "client",
            ClientSecretHash = "secret",
            TokenConfig = new OAuthClientTokenConfiguration
            {
                Values =
                [
                    new OAuthClientTokenClaimValue { Type = TokenClaimTypes.Issuer, Value = "issuer" },
                    new OAuthClientTokenClaimValue { Type = TokenClaimTypes.Audience, Value = "audience" }
                ]
            }
        };

        var result = await ctx.Service.IssueTokenAsync(
            new TokenRequest("client_credentials", null, null, null, "client", "secret"),
            ctx.Tenant.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeFalse();
        result.Value.Token!.AccessToken.Should().Be("access-client");
        ctx.ChallengeService.Created.Should().BeFalse();
    }

    [Theory]
    [InlineData(AccountAuthTwoFactorModes.Disabled, false, false)]
    [InlineData(AccountAuthTwoFactorModes.Optional, false, false)]
    [InlineData(AccountAuthTwoFactorModes.Optional, true, true)]
    [InlineData(AccountAuthTwoFactorModes.Required, false, true)]
    public async Task Password_grant_applies_schema_policy(
        string mode,
        bool userEnabledTwoFactor,
        bool expectedChallenge)
    {
        var ctx = TestContext.Create();
        ctx.Credentials = new CredentialValidationResult(
            "user-1",
            "user@example.com",
            "user@example.com",
            "schema-1",
            [UserRoles.User]);
        ctx.Schema = SchemaWithMode(mode);
        ctx.Settings = new UserTwoFactorSettings(userEnabledTwoFactor, TwoFactorMethods.EmailCode, userEnabledTwoFactor);

        var result = await ctx.Service.IssueTokenAsync(
            new TokenRequest("password", "user@example.com", "password", null, null, null),
            ctx.Tenant.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().Be(expectedChallenge);
        if (expectedChallenge)
        {
            result.Value.Token.Should().BeNull();
            result.Value.Challenge!.ChallengeId.Should().Be("challenge-1");
        }
        else
        {
            result.Value.Token!.AccessToken.Should().Be("access-user-1");
            result.Value.Challenge.Should().BeNull();
        }
    }

    private static AccountJsonSchema SchemaWithMode(string mode) =>
        new()
        {
            Id = "schema-1",
            TenantId = "tenant-1",
            Key = "default",
            DisplayName = "Default",
            SchemaJson = "{}",
            ConfigJson = $$"""
            {
              "auth": {
                "twoFactor": {
                  "mode": "{{mode}}",
                  "allowedMethods": ["email_code"],
                  "defaultMethod": "email_code"
                }
              }
            }
            """,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private sealed class TestContext
    {
        private TestContext()
        {
            TenantLookup = new FakeTenantLookup(Tenant);
            CredentialValidator = new FakeCredentialValidator(this);
            UserWriter = new FakeUserWriter();
            SchemaRepository = new FakeSchemaRepository(this);
            OAuthRepository = new FakeOAuthRepository(this);
            RefreshRepository = new FakeRefreshRepository();
            SettingsService = new FakeSettingsService(this);
            ChallengeService = new FakeChallengeService();
            AccessTokenIssuer = new FakeAccessTokenIssuer();
            SigningKeyResolver = new FakeSigningKeyResolver();
            PasswordHasher = new FakePasswordHasher();

            Service = new AuthenticationAppService(
                TenantLookup,
                CredentialValidator,
                UserWriter,
                SchemaRepository,
                OAuthRepository,
                RefreshRepository,
                SettingsService,
                ChallengeService,
                AccessTokenIssuer,
                SigningKeyResolver,
                PasswordHasher,
                Options.Create(new JwtOptions { SigningKey = "12345678901234567890123456789012" }),
                Options.Create(new RegistrationOptions()));
        }

        public Tenant Tenant { get; } = new()
        {
            Id = "tenant-1",
            Name = "Tenant",
            DatabaseName = "tenant_1",
            Active = true
        };

        public CredentialValidationResult? Credentials { get; set; }
        public AccountJsonSchema? Schema { get; set; }
        public OAuthClient? OAuthClient { get; set; }
        public UserTwoFactorSettings Settings { get; set; } = new(false, TwoFactorMethods.EmailCode, false);

        public FakeTenantLookup TenantLookup { get; }
        public FakeCredentialValidator CredentialValidator { get; }
        public FakeUserWriter UserWriter { get; }
        public FakeSchemaRepository SchemaRepository { get; }
        public FakeOAuthRepository OAuthRepository { get; }
        public FakeRefreshRepository RefreshRepository { get; }
        public FakeSettingsService SettingsService { get; }
        public FakeChallengeService ChallengeService { get; }
        public FakeAccessTokenIssuer AccessTokenIssuer { get; }
        public FakeSigningKeyResolver SigningKeyResolver { get; }
        public FakePasswordHasher PasswordHasher { get; }
        public AuthenticationAppService Service { get; }

        public static TestContext Create() => new();
    }

    private sealed class FakeTenantLookup(Tenant tenant) : ITenantLookup
    {
        public Task<Tenant?> FindActiveByIdAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Tenant?>(tenantId == tenant.Id ? tenant : null);

        public Task<IReadOnlyList<Tenant>> ListActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Tenant>>([tenant]);
    }

    private sealed class FakeCredentialValidator(TestContext context) : IUserCredentialValidator
    {
        public Task<CredentialValidationOutcome> ValidateAsync(
            string tenantId,
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(context.Credentials is null
                ? CredentialValidationOutcome.Failed(CredentialValidationFailure.InvalidCredentials)
                : CredentialValidationOutcome.Succeeded(context.Credentials));
    }

    private sealed class FakeSchemaRepository(TestContext context) : IAccountJsonSchemaRepository
    {
        public Task<IReadOnlyList<AccountJsonSchema>> ListByTenantAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountJsonSchema>>([]);
        public Task<AccountJsonSchema?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(context.Schema);
        public Task<AccountJsonSchema?> GetByKeyAsync(string tenantId, string key, CancellationToken cancellationToken = default) => Task.FromResult(context.Schema);
        public Task<AccountJsonSchema?> GetDefaultAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult(context.Schema);
        public Task InsertAsync(AccountJsonSchema entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceAsync(AccountJsonSchema entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearDefaultFlagForTenantAsync(string tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeOAuthRepository(TestContext context) : IOAuthClientRepository
    {
        public Task<IReadOnlyList<OAuthClient>> ListAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OAuthClient>>([]);
        public Task<OAuthClient?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(context.OAuthClient);
        public Task<OAuthClient?> FindByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken = default) => Task.FromResult(context.OAuthClient);
        public Task InsertAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> DeactivateAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeSettingsService(TestContext context) : ITwoFactorSettingsService
    {
        public Task<UserTwoFactorSettings> GetUserSettingsAsync(string tenantId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(context.Settings);
        public Task SetUserEmailTwoFactorAsync(string tenantId, string userId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<UserTwoFactorSettings> GetPlatformAdminSettingsAsync(string adminId, CancellationToken cancellationToken = default) => Task.FromResult(new UserTwoFactorSettings(false, TwoFactorMethods.EmailCode, false));
        public Task SetPlatformAdminEmailTwoFactorAsync(string adminId, bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeChallengeService : ITwoFactorChallengeService
    {
        public bool Created { get; private set; }

        public Task<TwoFactorChallengeDto> CreateEmailChallengeAsync(TwoFactorChallengeSubject subject, string purpose, CancellationToken cancellationToken = default)
        {
            Created = true;
            return Task.FromResult(new TwoFactorChallengeDto("challenge-1", TwoFactorMethods.EmailCode, 300, "u***@example.com"));
        }

        public Task<ServiceResult<TwoFactorChallenge>> VerifyAsync(string challengeId, string code, string? tenantId, string subjectType, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<TwoFactorChallenge>.Fail(501, "not implemented"));
    }

    private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
    {
        public IssueTokenResponse CreateAccessToken(string subjectId, string tenantId, TokenIssuanceProfile profile, TenantSigningKeyMaterial signingKey, IEnumerable<Claim>? additionalClaims = null) =>
            new($"access-{subjectId}", "Bearer", 3600);

        public IssueTokenResponse CreatePlatformAdminAccessToken(string adminId, string email, IEnumerable<Claim>? additionalClaims = null) =>
            new($"platform-{adminId}", "Bearer", 3600);
    }

    private sealed class FakeSigningKeyResolver : ITenantSigningKeyResolver
    {
        public Task<TenantSigningKeyMaterial> ResolveSigningKeyAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantSigningKeyMaterial("", "", "", 1));

        public string? ResolvePublicKeyJwk(string tenantId) => null;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plainTextPassword) => plainTextPassword;
        public bool Verify(string plainTextPassword, string storedHash) => plainTextPassword == storedHash;
    }

    private sealed class FakeRefreshRepository : IRefreshTokenRepository
    {
        public Task<(string PlainToken, RefreshToken Entity)> CreateAsync(string tenantId, string subjectId, string subjectType, IReadOnlyList<string> roles, IReadOnlyList<string> scopes, DateTime expiresAtUtc, int maxUses, CancellationToken cancellationToken = default) =>
            Task.FromResult(("refresh", new RefreshToken { Id = "rt", SubjectId = subjectId, SubjectType = subjectType, TokenHash = "hash" }));

        public Task<RefreshToken?> FindActiveByPlainTokenAsync(string tenantId, string plainToken, CancellationToken cancellationToken = default) => Task.FromResult<RefreshToken?>(null);
        public Task<bool> TryIncrementUseCountAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RevokeBySubjectAsync(string tenantId, string subjectId, string subjectType, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUserWriter : IUserAccountWriter
    {
        public Task<bool> EmailExistsAsync(string tenantId, string email, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> UsernameExistsAsync(string tenantId, string username, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string> InsertAsync(string tenantId, string documentJson, CancellationToken cancellationToken = default) => Task.FromResult("user");
        public Task<string?> GetUserDocumentJsonAsync(string tenantId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ReplaceUserDocumentAsync(string tenantId, string userId, string documentJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> EmailTakenByOtherUserAsync(string tenantId, string email, string excludeUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> UsernameTakenByOtherUserAsync(string tenantId, string username, string excludeUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task MarkExpiryReminderSentAsync(string tenantId, string userId, int reminderKind, DateTime sentUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryMarkRegistrationExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> TryConfirmEmailAsync(string tenantId, string email, string secretPlain, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string?> GetUserStatusAsync(string tenantId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(AccountStatuses.Active);
        public Task<IReadOnlyList<string>> ListUserDocumentsJsonAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string?> TryGetActiveUserEmailAsync(string tenantId, string? email, string? username, CancellationToken cancellationToken = default) => Task.FromResult(email ?? username);
        public Task<string?> TryGetActiveUserIdByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default) => Task.FromResult<string?>("user-1");
        public Task<bool> TrySetPasswordResetTokenAsync(string tenantId, string email, string tokenHash, DateTime expiresAtUtc, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> TryResetPasswordAsync(string tenantId, string email, string tokenPlain, string newPasswordHash, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
