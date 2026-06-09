namespace Lightcode.Registration.Application.Contracts.Tenants;

public sealed record TenantCreatedDto(
    string Id,
    string Name,
    string DatabaseName,
    string OAuthClientId);
