namespace Lightcode.Registration.Application.Contracts.Tenants;

public sealed record CreateTenantCommand(string? Name, string? AdminEmail, string? ProvisioningKeyFromRequest);
