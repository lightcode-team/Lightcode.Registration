namespace Lightcode.Registration.Application.Configuration;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Se true, o Worker envia email real via SMTP (credenciais na collection Settings do tenant); caso contrário apenas logging.</summary>
    public bool UseSmtp { get; set; }
}
