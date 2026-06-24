using FluentAssertions;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.Services;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class HostedAuthenticationAppServiceTests
{
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

    [Fact]
    public async Task Start_rejects_redirect_uri_not_registered_for_client()
    {
        var context = new Context();

        var result = await context.Service.StartAsync(CreateRequest(redirectUri: "https://evil.example/callback"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        context.Transactions.Value.Should().BeNull();
        context.Audit.Items.Should().Contain(x => x.EventType == AuthAuditEventTypes.HostedAuthorizationRejected);
    }

    [Fact]
    public async Task Start_preserves_nonce_and_validates_scope_before_login()
    {
        var context = new Context();

        var accepted = await context.Service.StartAsync(CreateRequest(nonce: "nonce-1", scope: "openid email"));
        var rejected = await context.Service.StartAsync(CreateRequest(scope: "admin"));

        accepted.IsSuccess.Should().BeTrue();
        accepted.Value!.Nonce.Should().Be("nonce-1");
        accepted.Value.Scope.Should().Be("openid email");
        rejected.IsSuccess.Should().BeFalse();
        context.Sessions.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_without_two_factor_creates_code_and_exchange_is_single_use()
    {
        var context = new Context();
        var started = await context.Service.StartAsync(CreateRequest(state: "state-1"));

        var login = await context.Service.LoginAsync(started.Value!.Id, "user", "password");

        login.IsSuccess.Should().BeTrue();
        login.Value!.Completed.Should().BeTrue();
        login.Value.RedirectUrl.Should().Contain("state=state-1");
        context.Sessions.Value!.Stage.Should().Be(HostedAuthStages.Completed);

        var code = ReadQueryValue(login.Value.RedirectUrl!, "code");
        var request = new TokenRequest(
            TokenGrantTypes.AuthorizationCode,
            null,
            null,
            null,
            "client-1",
            null,
            code,
            "https://app.example/callback",
            Verifier);

        var exchanged = await context.Service.ExchangeAuthorizationCodeAsync(request, "tenant-1");
        var replay = await context.Service.ExchangeAuthorizationCodeAsync(request, "tenant-1");

        exchanged.IsSuccess.Should().BeTrue();
        exchanged.Value!.Token!.AccessToken.Should().Be("access-user-1");
        replay.IsSuccess.Should().BeFalse();
        context.Authentication.IssuedCount.Should().Be(1);
        context.Codes.Value!.Nonce.Should().Be("nonce-1");
        context.Audit.Items.Should().Contain(x => x.EventType == AuthAuditEventTypes.AuthorizationCodeConsumed);
    }

    [Fact]
    public async Task Exchange_rejects_wrong_pkce_verifier_without_consuming_code()
    {
        var context = new Context();
        var started = await context.Service.StartAsync(CreateRequest());
        var login = await context.Service.LoginAsync(started.Value!.Id, "user", "password");
        var code = ReadQueryValue(login.Value!.RedirectUrl!, "code");

        var wrongVerifier = new string('x', 48);
        var rejected = await context.Service.ExchangeAuthorizationCodeAsync(
            new TokenRequest(TokenGrantTypes.AuthorizationCode, null, null, null, "client-1", null,
                code, "https://app.example/callback", wrongVerifier),
            "tenant-1");

        rejected.IsSuccess.Should().BeFalse();
        context.Codes.Value!.ConsumedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Concurrent_exchange_consumes_authorization_code_once()
    {
        var context = new Context();
        var started = await context.Service.StartAsync(CreateRequest());
        var login = await context.Service.LoginAsync(started.Value!.Id, "user", "password");
        var code = ReadQueryValue(login.Value!.RedirectUrl!, "code");
        var request = new TokenRequest(TokenGrantTypes.AuthorizationCode, null, null, null, "client-1", null,
            code, "https://app.example/callback", Verifier);

        var results = await Task.WhenAll(
            context.Service.ExchangeAuthorizationCodeAsync(request, "tenant-1"),
            context.Service.ExchangeAuthorizationCodeAsync(request, "tenant-1"));

        results.Count(x => x.IsSuccess).Should().Be(1);
        context.Authentication.IssuedCount.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_two_factor_confirmation_completes_once()
    {
        var context = new Context { RequiresTwoFactor = true };
        var started = await context.Service.StartAsync(CreateRequest());
        var login = await context.Service.LoginAsync(started.Value!.Id, "user", "password");

        login.Value!.Completed.Should().BeFalse();

        var results = await Task.WhenAll(
            context.Service.ConfirmTwoFactorAsync(started.Value!.Id, "123456"),
            context.Service.ConfirmTwoFactorAsync(started.Value!.Id, "123456"));

        results.Count(x => x.IsSuccess).Should().Be(1);
        context.Codes.InsertCount.Should().Be(1);
    }

    [Fact]
    public async Task Login_creates_sso_session_and_second_client_reuses_it()
    {
        var context = new Context();
        var first = await context.Service.StartAsync(CreateRequest());
        var login = await context.Service.LoginAsync(
            first.Value!.Id,
            "user",
            "password",
            new HostedSsoContext(null, "ua-1", "ip-1"));

        login.Value!.SsoSessionId.Should().NotBeNullOrWhiteSpace();
        context.SsoSessions.Items.Should().ContainKey(login.Value.SsoSessionId!);

        var second = await context.Service.StartAsync(CreateRequest(state: "state-2"));
        var reused = await context.Service.CompleteFromSsoAsync(
            second.Value!.Id,
            login.Value.SsoSessionId,
            new HostedSsoContext(login.Value.SsoSessionId, "ua-1", "ip-1"));

        reused.IsSuccess.Should().BeTrue();
        reused.Value!.Completed.Should().BeTrue();
        reused.Value.RedirectUrl.Should().Contain("state=state-2");
        context.Audit.Items.Should().Contain(x => x.EventType == AuthAuditEventTypes.SsoSessionReused);
    }

    [Fact]
    public async Task Prompt_login_does_not_reuse_sso_session()
    {
        var context = new Context();
        var first = await context.Service.StartAsync(CreateRequest());
        var login = await context.Service.LoginAsync(first.Value!.Id, "user", "password", new HostedSsoContext(null, null, null));

        var forced = await context.Service.StartAsync(CreateRequest(prompt: "login"));
        var reused = await context.Service.CompleteFromSsoAsync(
            forced.Value!.Id,
            login.Value!.SsoSessionId,
            new HostedSsoContext(login.Value.SsoSessionId, null, null));

        reused.IsSuccess.Should().BeFalse();
        context.Codes.InsertCount.Should().Be(1);
    }

    [Fact]
    public async Task Prompt_none_without_sso_returns_login_required()
    {
        var context = new Context();
        var started = await context.Service.StartAsync(CreateRequest(prompt: "none"));

        var result = await context.Service.CompleteFromSsoAsync(started.Value!.Id, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("login_required");
        context.Audit.Items.Should().Contain(x => x.EventType == AuthAuditEventTypes.SsoPromptNoneFailed);
    }

    [Fact]
    public async Task Max_age_expired_blocks_sso_reuse()
    {
        var context = new Context();
        var first = await context.Service.StartAsync(CreateRequest());
        var login = await context.Service.LoginAsync(first.Value!.Id, "user", "password", new HostedSsoContext(null, null, null));
        context.SsoSessions.Items[login.Value!.SsoSessionId!].AuthTimeUtc = DateTime.UtcNow.AddMinutes(-5);

        var started = await context.Service.StartAsync(CreateRequest(maxAge: "1"));
        var result = await context.Service.CompleteFromSsoAsync(
            started.Value!.Id,
            login.Value.SsoSessionId,
            new HostedSsoContext(login.Value.SsoSessionId, null, null));

        result.IsSuccess.Should().BeFalse();
    }

    private static HostedAuthorizationRequest CreateRequest(
        string redirectUri = "https://app.example/callback",
        string state = "state-1",
        string nonce = "nonce-1",
        string? scope = "openid",
        string? prompt = null,
        string? maxAge = null) =>
        new(
            "code",
            "tenant-1",
            "client-1",
            redirectUri,
            state,
            nonce,
            scope,
            OAuthPkce.CreateChallenge(Verifier),
            "S256",
            prompt,
            maxAge);

    private static string ReadQueryValue(string url, string key)
    {
        var query = new Uri(url).Query.TrimStart('?').Split('&');
        var pair = query.Select(value => value.Split('=', 2))
            .Single(value => Uri.UnescapeDataString(value[0]) == key);
        return Uri.UnescapeDataString(pair[1]);
    }

    private sealed class Context
    {
        public Context()
        {
            Authentication = new FakeAuthentication(this);
            Service = new HostedAuthenticationAppService(
                Authentication,
                OAuthClients,
                Transactions,
                Sessions,
                Codes,
                Audit,
                SsoSessions,
                UserAccounts,
                Challenges,
                ChallengeRepository,
                Tokens);
        }

        public bool RequiresTwoFactor { get; init; }
        public FakeAuthentication Authentication { get; }
        public FakeOAuthClients OAuthClients { get; } = new();
        public FakeTransactions Transactions { get; } = new();
        public FakeSessions Sessions { get; } = new();
        public FakeCodes Codes { get; } = new();
        public FakeAudit Audit { get; } = new();
        public FakeSsoSessions SsoSessions { get; } = new();
        public FakeUserAccounts UserAccounts { get; } = new();
        public FakeChallenges Challenges { get; } = new();
        public FakeChallengeRepository ChallengeRepository { get; } = new();
        public FakeTokens Tokens { get; } = new();
        public HostedAuthenticationAppService Service { get; }
    }

    private sealed class FakeAuthentication(Context context) : IAuthenticationAppService
    {
        public int IssuedCount { get; private set; }

        public Task<ServiceResult<HostedPasswordAuthenticationResult>> BeginHostedPasswordAuthenticationAsync(
            string? username, string? password, string tenantId, CancellationToken cancellationToken = default)
        {
            var challenge = context.RequiresTwoFactor
                ? new TwoFactorChallengeDto("challenge", "email_code", 300, "u***@example.com")
                : null;

            return Task.FromResult(ServiceResult<HostedPasswordAuthenticationResult>.Ok(
                new HostedPasswordAuthenticationResult("user-1", "user@example.com", "user", challenge)));
        }

        public Task<ServiceResult<HostedPasswordAuthenticationResult>> ConfirmHostedTwoFactorAsync(
            ConfirmTwoFactorRequest request, string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<HostedPasswordAuthenticationResult>.Ok(
                new HostedPasswordAuthenticationResult("user-1", "user@example.com", "user", null, "email_code")));

        public Task<ServiceResult<AuthTokenResponse>> IssueHostedIdentityTokenAsync(
            string subjectId, string? mfaMethod, string tenantId, CancellationToken cancellationToken = default)
        {
            IssuedCount++;
            return Task.FromResult(ServiceResult<AuthTokenResponse>.Ok(
                AuthTokenResponse.Issued(new IssueTokenResponse($"access-{subjectId}", "Bearer", 3600, "refresh"))));
        }

        public Task<ServiceResult<AuthTokenResponse>> IssueTokenAsync(TokenRequest request, string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<AuthTokenResponse>.Fail(501, "not used"));

        public Task<ServiceResult<AuthTokenResponse>> ConfirmTwoFactorAsync(ConfirmTwoFactorRequest request, string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<AuthTokenResponse>.Fail(501, "not used"));
    }

    private sealed class FakeOAuthClients : IOAuthClientRepository
    {
        private readonly OAuthClient client = new()
        {
            Id = "oauth-1",
            ClientId = "client-1",
            RedirectUris = ["https://app.example/callback"],
            AllowedScopes = ["openid", "email"],
            Active = true
        };

        public Task<OAuthClient?> FindByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<OAuthClient?>(tenantId == "tenant-1" && clientId == client.ClientId ? client : null);
        public Task<IReadOnlyList<OAuthClient>> ListAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OAuthClient>>([client]);
        public Task<OAuthClient?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult<OAuthClient?>(client);
        public Task InsertAsync(string tenantId, OAuthClient value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceAsync(string tenantId, OAuthClient value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> DeactivateAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeTransactions : IHostedAuthTransactionRepository
    {
        public HostedAuthTransaction? Value { get; private set; }
        public Task InsertAsync(HostedAuthTransaction transaction, CancellationToken cancellationToken = default) { Value = transaction; return Task.CompletedTask; }
        public Task<HostedAuthTransaction?> FindActiveAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Value?.Id == id && Value.ExpiresAtUtc > DateTime.UtcNow ? Value : null);
    }

    private sealed class FakeSessions : IHostedAuthSessionRepository
    {
        private readonly object sync = new();
        public HostedAuthSession? Value { get; private set; }
        public Task InsertAsync(HostedAuthSession session, CancellationToken cancellationToken = default) { Value = session; return Task.CompletedTask; }
        public Task<HostedAuthSession?> FindActiveByTransactionIdAsync(string transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Value?.TransactionId == transactionId && Value.ExpiresAtUtc > DateTime.UtcNow ? Value : null);
        public Task ReplaceAsync(HostedAuthSession session, CancellationToken cancellationToken = default) { Value = session; return Task.CompletedTask; }
        public Task<bool> TryBeginCompletionAsync(string id, string expectedStage, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                if (Value?.Id != id || Value.Stage != expectedStage)
                    return Task.FromResult(false);

                Value.Stage = HostedAuthStages.Completing;
                return Task.FromResult(true);
            }
        }
    }

    private sealed class FakeCodes : IAuthorizationCodeRepository
    {
        private readonly object sync = new();
        public AuthorizationCodeGrant? Value { get; private set; }
        public int InsertCount { get; private set; }
        public Task InsertAsync(AuthorizationCodeGrant grant, CancellationToken cancellationToken = default)
        {
            Value = grant;
            InsertCount++;
            return Task.CompletedTask;
        }

        public Task<AuthorizationCodeGrant?> TryConsumeAsync(string tenantId, string codeHash, string clientId, string redirectUri, string codeChallenge, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                if (Value is null || Value.ConsumedAtUtc is not null || Value.TenantId != tenantId || Value.CodeHash != codeHash
                    || Value.ClientId != clientId || Value.RedirectUri != redirectUri || Value.CodeChallenge != codeChallenge)
                    return Task.FromResult<AuthorizationCodeGrant?>(null);

                var result = Value;
                Value.ConsumedAtUtc = DateTime.UtcNow;
                return Task.FromResult<AuthorizationCodeGrant?>(result);
            }
        }
    }

    private sealed class FakeAudit : IAuthAuditLogRepository
    {
        public List<AuthAuditLog> Items { get; } = [];
        public Task InsertAsync(AuthAuditLog entry, CancellationToken cancellationToken = default)
        {
            Items.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSsoSessions : ISsoSessionRepository
    {
        public Dictionary<string, SsoSession> Items { get; } = new(StringComparer.Ordinal);

        public Task InsertAsync(SsoSession session, CancellationToken cancellationToken = default)
        {
            Items[session.Id] = session;
            return Task.CompletedTask;
        }

        public Task<SsoSession?> FindActiveAsync(
            string id,
            string tenantId,
            DateTime idleCutoffUtc,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            if (!Items.TryGetValue(id, out var session)
                || session.TenantId != tenantId
                || session.RevokedAtUtc is not null
                || session.ExpiresAtUtc <= nowUtc
                || session.LastSeenAtUtc <= idleCutoffUtc)
                return Task.FromResult<SsoSession?>(null);

            return Task.FromResult<SsoSession?>(session);
        }

        public Task TouchAsync(string id, DateTime lastSeenAtUtc, CancellationToken cancellationToken = default)
        {
            if (Items.TryGetValue(id, out var session))
                session.LastSeenAtUtc = lastSeenAtUtc;
            return Task.CompletedTask;
        }

        public Task RevokeAsync(string id, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
        {
            if (Items.TryGetValue(id, out var session))
                session.RevokedAtUtc = revokedAtUtc;
            return Task.CompletedTask;
        }

        public Task RevokeBySubjectAsync(string tenantId, string subjectId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
        {
            foreach (var session in Items.Values.Where(x => x.TenantId == tenantId && x.SubjectId == subjectId))
                session.RevokedAtUtc = revokedAtUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserAccounts : IUserAccountWriter
    {
        public string Status { get; set; } = AccountStatuses.Active;

        public Task<string?> GetUserStatusAsync(string tenantId, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(Status);

        public Task<bool> EmailExistsAsync(string tenantId, string email, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> UsernameExistsAsync(string tenantId, string username, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string> InsertAsync(string tenantId, string documentJson, CancellationToken cancellationToken = default) => Task.FromResult("user-1");
        public Task<string?> GetUserDocumentJsonAsync(string tenantId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ReplaceUserDocumentAsync(string tenantId, string userId, string documentJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> EmailTakenByOtherUserAsync(string tenantId, string email, string excludeUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> UsernameTakenByOtherUserAsync(string tenantId, string username, string excludeUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task MarkExpiryReminderSentAsync(string tenantId, string userId, int reminderKind, DateTime sentUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryMarkRegistrationExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> TryConfirmEmailAsync(string tenantId, string email, string secretPlain, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> ListUserDocumentsJsonAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string?> TryGetActiveUserEmailAsync(string tenantId, string? email, string? username, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> TryGetActiveUserIdByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<bool> TrySetPasswordResetTokenAsync(string tenantId, string email, string tokenHash, DateTime expiresAtUtc, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> TryResetPasswordAsync(string tenantId, string email, string tokenPlain, string newPasswordHash, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeChallenges : ITwoFactorChallengeService
    {
        public Task<TwoFactorChallengeDto> CreateEmailChallengeAsync(TwoFactorChallengeSubject subject, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TwoFactorChallengeDto("challenge", "email_code", 300, "u***@example.com"));
        public Task<ServiceResult<TwoFactorChallenge>> VerifyAsync(string challengeId, string code, string? tenantId, string subjectType, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(ServiceResult<TwoFactorChallenge>.Fail(501, "not used"));
    }

    private sealed class FakeChallengeRepository : ITwoFactorChallengeRepository
    {
        public Task InsertAsync(TwoFactorChallenge challenge, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidatePendingAsync(string? tenantId, string subjectType, string subjectId, string purpose, string method, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TwoFactorChallenge?> FindAsync(string challengeId, string? tenantId, string subjectType, string purpose, CancellationToken cancellationToken = default) => Task.FromResult<TwoFactorChallenge?>(null);
        public Task<bool> TryMarkUsedAsync(string challengeId, string? tenantId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> TryIncrementAttemptsAsync(string challengeId, string? tenantId, int maxAttempts, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeTokens : ISecureTokenGenerator
    {
        public string GenerateAuthorizationCode() => "authorization-code";
        public string GenerateRefreshToken() => "refresh";
        public string GenerateClientSecret() => "secret";
        public string GenerateEmailConfirmationCode() => "123456";
        public string GenerateEmailConfirmationToken() => "email-token";
        public string GeneratePasswordResetToken() => "reset-token";
    }
}
