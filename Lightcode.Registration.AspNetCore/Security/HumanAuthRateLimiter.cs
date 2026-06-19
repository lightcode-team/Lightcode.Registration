using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Lightcode.Registration.AspNetCore.Security;

public sealed class HumanAuthRateLimiter(ILogger<HumanAuthRateLimiter> logger)
{
    private static readonly ConcurrentDictionary<string, Counter> Counters = new(StringComparer.Ordinal);

    public IActionResult? LimitPasswordGrant(HttpContext context, string? tenantId, string? usernameOrEmail) =>
        TryConsume(
            context,
            "auth_password_grant",
            permitLimit: 10,
            window: TimeSpan.FromMinutes(1),
            tenantId,
            usernameOrEmail);

    public IActionResult? LimitPlatformPasswordGrant(HttpContext context, string? email) =>
        TryConsume(
            context,
            "platform_password_grant",
            permitLimit: 10,
            window: TimeSpan.FromMinutes(1),
            email);

    public IActionResult? LimitTwoFactorConfirmation(
        HttpContext context,
        string policy,
        string? tenantId,
        string? challengeId) =>
        TryConsume(
            context,
            policy,
            permitLimit: 5,
            window: TimeSpan.FromMinutes(5),
            tenantId,
            challengeId);

    public IActionResult? LimitTwoFactorManagement(
        HttpContext context,
        string? tenantId,
        string? subjectId) =>
        TryConsume(
            context,
            "two_factor_management",
            permitLimit: 5,
            window: TimeSpan.FromMinutes(10),
            tenantId,
            subjectId);

    public IActionResult? LimitAccountRecovery(
        HttpContext context,
        string? tenantId,
        string? usernameOrEmail) =>
        TryConsume(
            context,
            "account_recovery",
            permitLimit: 5,
            window: TimeSpan.FromMinutes(10),
            tenantId,
            usernameOrEmail);

    private IActionResult? TryConsume(
        HttpContext context,
        string policy,
        int permitLimit,
        TimeSpan window,
        params string?[] parts)
    {
        var now = DateTimeOffset.UtcNow;
        var key = BuildKey(context, policy, parts);
        var counter = Counters.AddOrUpdate(
            key,
            _ => new Counter(now, 1),
            (_, current) =>
            {
                if (now - current.WindowStartedAt >= window)
                    return new Counter(now, 1);

                return current with { Count = current.Count + 1 };
            });

        if (counter.Count <= permitLimit)
            return null;

        logger.LogWarning(
            "human_auth_rate_limit_exceeded Policy={Policy} Path={Path} Ip={Ip}",
            policy,
            context.Request.Path.Value,
            ResolveIp(context));

        return new ObjectResult(new
        {
            Error = true,
            Errors = new[] { "Muitas tentativas. Tente novamente mais tarde." },
            StatusCode = StatusCodes.Status429TooManyRequests,
            Data = (object?)null
        })
        {
            StatusCode = StatusCodes.Status429TooManyRequests
        };
    }

    private static string BuildKey(HttpContext context, string policy, IReadOnlyList<string?> parts)
    {
        var normalizedParts = parts
            .Select(NormalizePart)
            .DefaultIfEmpty("-");

        return string.Join(':', [policy, ResolveIp(context), .. normalizedParts]);
    }

    private static string ResolveIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string NormalizePart(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim().ToLowerInvariant();

    private sealed record Counter(DateTimeOffset WindowStartedAt, int Count);
}
