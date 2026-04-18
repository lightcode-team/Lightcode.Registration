namespace Lightcode.Registration.Application.Configuration;

public sealed class MasterOptions
{
    public const string SectionName = "Master";

    public string? ProvisioningApiKey { get; set; }
}
