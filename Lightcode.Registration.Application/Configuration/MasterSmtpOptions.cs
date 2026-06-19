namespace Lightcode.Registration.Application.Configuration;

public sealed class MasterSmtpOptions
{
    public const string SectionName = "MasterSmtp";

    public bool UseSmtp { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Usuario { get; set; } = string.Empty;

    public string Senha { get; set; } = string.Empty;

    public string EmailRemetente { get; set; } = string.Empty;

    public string NomeRemetente { get; set; } = "Lightcode";

    public bool UsarSsl { get; set; }
}
