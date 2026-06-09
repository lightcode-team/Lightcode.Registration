namespace Lightcode.Registration.Application.Configuration;

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    /// <summary>URL pública da API (ex.: https://api.example.com) para links de confirmação por email (modo Link).</summary>
    public string? PublicApiBaseUrl { get; set; }
}
