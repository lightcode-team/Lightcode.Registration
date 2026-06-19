using FluentAssertions;
using Lightcode.Registration.AspNetCore.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class HumanAuthRateLimiterTests
{
    [Fact]
    public void Password_grant_limit_uses_username_or_email_in_key()
    {
        var limiter = CreateLimiter();
        var context = CreateContext();

        for (var i = 0; i < 10; i++)
            limiter.LimitPasswordGrant(context, "tenant-1", "user-a@example.com").Should().BeNull();

        limiter.LimitPasswordGrant(context, "tenant-1", "user-a@example.com").Should().NotBeNull();
        limiter.LimitPasswordGrant(context, "tenant-1", "user-b@example.com").Should().BeNull();
    }

    [Fact]
    public void Confirm_two_factor_limit_uses_challenge_id_in_key()
    {
        var limiter = CreateLimiter();
        var context = CreateContext();

        for (var i = 0; i < 5; i++)
            limiter.LimitTwoFactorConfirmation(context, "auth_confirm_2fa", "tenant-1", "challenge-a").Should().BeNull();

        limiter.LimitTwoFactorConfirmation(context, "auth_confirm_2fa", "tenant-1", "challenge-a").Should().NotBeNull();
        limiter.LimitTwoFactorConfirmation(context, "auth_confirm_2fa", "tenant-1", "challenge-b").Should().BeNull();
    }

    private static HumanAuthRateLimiter CreateLimiter() =>
        new(NullLogger<HumanAuthRateLimiter>.Instance);

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        context.Request.Path = "/api/auth/token";
        return context;
    }
}
