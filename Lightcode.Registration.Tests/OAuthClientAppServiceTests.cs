using FluentAssertions;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Contracts.OAuthClients;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.Services;
using Lightcode.Registration.Domain.Entities;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class OAuthClientAppServiceTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task Create_persists_notify_email_and_returns_secret_status_and_dates()
    {
        var ctx = TestContext.Create();
        var request = CreateRequest(notifyEmail: " admin@example.com ");

        var result = await ctx.Service.CreateAsync(TenantId, request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ClientSecret.Should().Be("plain-secret");
        result.Value.NotifyEmail.Should().Be("admin@example.com");
        result.Value.Active.Should().BeTrue();
        result.Value.CreatedAtUtc.Should().NotBe(default);
        result.Value.UpdatedAtUtc.Should().Be(result.Value.CreatedAtUtc);

        var entity = ctx.Repository.Clients.Should().ContainSingle().Subject;
        entity.NotifyEmail.Should().Be("admin@example.com");
        entity.ClientSecretHash.Should().Be("hashed:plain-secret");
        ctx.EmailPublisher.Messages.Should().ContainSingle()
            .Which.To.Should().Be("admin@example.com");
    }

    [Fact]
    public async Task Get_by_id_returns_active_client_from_tenant()
    {
        var ctx = TestContext.Create();
        var client = ExistingClient();
        ctx.Repository.Clients.Add(client);

        var result = await ctx.Service.GetByIdAsync(TenantId, client.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(client.Id);
        result.Value.NotifyEmail.Should().Be(client.NotifyEmail);
    }

    [Fact]
    public async Task Update_by_id_updates_client_fields()
    {
        var ctx = TestContext.Create();
        var client = ExistingClient();
        ctx.Repository.Clients.Add(client);

        var result = await ctx.Service.UpdateByIdAsync(
            TenantId,
            client.Id,
            new UpdateOAuthClientRequest(
                " Updated Client ",
                " updated@example.com ",
                TokenConfig(accessMinutes: 45, refreshDays: 20, maxUses: 4),
                ["https://app.example.com/callback"],
                ["https://app.example.com/logout"],
                ["openid", "email"],
                true));

        result.IsSuccess.Should().BeTrue();
        result.Value!.DisplayName.Should().Be("Updated Client");
        result.Value.NotifyEmail.Should().Be("updated@example.com");
        result.Value.TokenConfig.AccessTokenExpirationMinutes.Should().Be(45);
        result.Value.TokenConfig.RefreshTokenExpirationDays.Should().Be(20);
        result.Value.TokenConfig.MaxRefreshTokenUses.Should().Be(4);
        result.Value.RedirectUris.Should().Equal("https://app.example.com/callback");
        result.Value.PostLogoutRedirectUris.Should().Equal("https://app.example.com/logout");
        result.Value.AllowedScopes.Should().Equal("openid", "email");
        result.Value.RequireConsent.Should().BeTrue();
        ctx.Repository.ReplacedClients.Should().ContainSingle().Which.Id.Should().Be(client.Id);
    }

    [Fact]
    public async Task Deactivate_by_id_marks_client_inactive()
    {
        var ctx = TestContext.Create();
        var client = ExistingClient();
        ctx.Repository.Clients.Add(client);

        var result = await ctx.Service.DeactivateByIdAsync(TenantId, client.Id);

        result.IsSuccess.Should().BeTrue();
        client.Active.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_id_returns_not_found()
    {
        var ctx = TestContext.Create();

        var result = await ctx.Service.GetByIdAsync(TenantId, "missing");

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at.example.com")]
    public async Task Invalid_notify_email_returns_bad_request(string notifyEmail)
    {
        var ctx = TestContext.Create();

        var result = await ctx.Service.CreateAsync(TenantId, CreateRequest(notifyEmail: notifyEmail));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        ctx.Repository.Clients.Should().BeEmpty();
    }

    private static CreateOAuthClientRequest CreateRequest(string? notifyEmail = "admin@example.com") =>
        new(
            "Client",
            notifyEmail,
            TokenConfig(),
            ["https://app.example.com/callback"],
            ["https://app.example.com/logout"],
            ["openid"],
            false);

    private static OAuthClientTokenConfigDto TokenConfig(
        int accessMinutes = 30,
        int refreshDays = 30,
        int maxUses = 5) =>
        new(
            accessMinutes,
            refreshDays,
            maxUses,
            [
                new OAuthClientTokenClaimValueDto(TokenClaimTypes.Issuer, "issuer"),
                new OAuthClientTokenClaimValueDto(TokenClaimTypes.Audience, "audience")
            ]);

    private static OAuthClient ExistingClient() =>
        new()
        {
            Id = "internal-id",
            ClientId = "client-id",
            ClientSecretHash = "hash",
            DisplayName = "Existing",
            NotifyEmail = "old@example.com",
            TokenConfig = new OAuthClientTokenConfiguration
            {
                Values =
                [
                    new OAuthClientTokenClaimValue { Type = TokenClaimTypes.Issuer, Value = "issuer" }
                ]
            },
            RedirectUris = ["https://old.example.com/callback"],
            PostLogoutRedirectUris = ["https://old.example.com/logout"],
            AllowedScopes = ["openid"],
            RequireConsent = false,
            Active = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

    private sealed class TestContext
    {
        private TestContext()
        {
            Service = new OAuthClientAppService(
                Repository,
                new FakePasswordHasher(),
                new FakeSecureTokenGenerator(),
                EmailPublisher);
        }

        public FakeOAuthClientRepository Repository { get; } = new();
        public FakeEmailEnqueuePublisher EmailPublisher { get; } = new();
        public OAuthClientAppService Service { get; }

        public static TestContext Create() => new();
    }

    private sealed class FakeOAuthClientRepository : IOAuthClientRepository
    {
        public List<OAuthClient> Clients { get; } = [];
        public List<OAuthClient> ReplacedClients { get; } = [];

        public Task<IReadOnlyList<OAuthClient>> ListAsync(string tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OAuthClient>>(Clients.Where(x => x.Active).ToList());

        public Task<OAuthClient?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Clients.FirstOrDefault(x => x.Id == id && x.Active));

        public Task<OAuthClient?> FindByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Clients.FirstOrDefault(x => x.ClientId == clientId && x.Active));

        public Task InsertAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default)
        {
            Clients.Add(client);
            return Task.CompletedTask;
        }

        public Task ReplaceAsync(string tenantId, OAuthClient client, CancellationToken cancellationToken = default)
        {
            ReplacedClients.Add(client);
            return Task.CompletedTask;
        }

        public Task<bool> DeactivateAsync(string tenantId, string id, CancellationToken cancellationToken = default)
        {
            var client = Clients.FirstOrDefault(x => x.Id == id && x.Active);
            if (client is null)
                return Task.FromResult(false);

            client.Active = false;
            client.UpdatedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string plainTextPassword) => $"hashed:{plainTextPassword}";

        public bool Verify(string plainTextPassword, string storedHash) => storedHash == Hash(plainTextPassword);
    }

    private sealed class FakeSecureTokenGenerator : ISecureTokenGenerator
    {
        public string GenerateRefreshToken() => "refresh-token";

        public string GenerateClientSecret() => "plain-secret";

        public string GenerateEmailConfirmationCode() => "123456";

        public string GenerateEmailConfirmationToken() => "email-token";

        public string GeneratePasswordResetToken() => "reset-token";
    }

    private sealed class FakeEmailEnqueuePublisher : IEmailEnqueuePublisher
    {
        public List<EmailDispatchQueueMessage> Messages { get; } = [];

        public Task<string> PublishSendAsync(EmailDispatchQueueMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult("message-id");
        }
    }
}
