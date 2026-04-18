namespace Lightcode.Registration.Application.Configuration;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Se true, envia email via SMTP; caso contrário usa apenas logging (útil em desenvolvimento).</summary>
    public bool UseSmtp { get; set; }

    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string User { get; set; } = "";

    public string Password { get; set; } = "";

    public string FromAddress { get; set; } = "noreply@localhost";

    public string FromName { get; set; } = "Lightcode Registration";
}
