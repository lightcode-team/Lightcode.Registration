namespace Lightcode.Registration.Application.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "Lightcode.Registration";
    public string Audience { get; set; } = "Lightcode.Registration";
    public int ExpirationMinutes { get; set; } = 120;
}
