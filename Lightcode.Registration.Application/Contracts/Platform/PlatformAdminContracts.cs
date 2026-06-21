using System.Text.Json.Serialization;
using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Contracts.Platform;

public sealed record InvitePlatformAdminRequest(string? Email, IReadOnlyList<string>? TenantIds = null);

public sealed record InvitePlatformAdminCommand(
    string? Email,
    IReadOnlyList<string>? TenantIds,
    string? ProvisioningKeyFromRequest);

public sealed record InvitePlatformAdminResult(
    string AdminId,
    string Email,
    string InviteToken,
    string? ActivationUrl,
    DateTime ExpiresAtUtc);

public sealed record ActivatePlatformAdminRequest(string? Token, string? Password);

public sealed record ActivatePlatformAdminResult(string AdminId, string Email);

public sealed record PlatformAdminTokenRequest(string? Email, string? Password);

public sealed record PlatformTenantDto(string Id, string Name, string Role);

public sealed record PlatformTenantTokenResult(
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("token")] IssueTokenResponse Token);

public sealed record PlatformAdminTwoFactorStatusResult(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("email_enabled")] bool EmailEnabled,
    [property: JsonPropertyName("preferred_method")] string PreferredMethod);
