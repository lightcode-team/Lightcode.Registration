namespace Lightcode.Registration.Application.Configuration;

public sealed class MasterOptions
{
    public const string SectionName = "Master";

    public string? ProvisioningApiKey { get; set; }

    /// <summary>Username do primeiro utilizador <c>admin</c> criado ao provisionar o tenant (normalizado em minúsculas).</summary>
    public string TenantBootstrapAdminUsername { get; set; } = "admin";

    /// <summary>Se preenchida (ex.: em Development), cria o utilizador admin inicial no tenant com esta password (mín. 8 caracteres).</summary>
    public string? TenantBootstrapAdminPassword { get; set; }
}
