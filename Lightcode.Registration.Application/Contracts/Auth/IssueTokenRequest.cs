namespace Lightcode.Registration.Application.Contracts.Auth;

public sealed record IssueTokenRequest(string UserId, string TenantId);
