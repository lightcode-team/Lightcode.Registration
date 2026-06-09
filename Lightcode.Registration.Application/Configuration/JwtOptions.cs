namespace Lightcode.Registration.Application.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "Lightcode.Registration";
    public string Audience { get; set; } = "Lightcode.Registration";
    public int ExpirationMinutes { get; set; } = 120;
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public int MaxRefreshTokenUses { get; set; } = 5;
}
