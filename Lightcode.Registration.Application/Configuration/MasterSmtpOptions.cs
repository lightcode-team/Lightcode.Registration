namespace Lightcode.Registration.Application.Configuration;

public sealed class MasterSmtpOptions
{
    public const string SectionName = "MasterSmtp";

    public bool UseSmtp { get; set; } = true;

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    public string Usuario { get; set; } = "contato.asfsolutions@gmail.com";

    public string Senha { get; set; } = "zurq wvwq rckk xvom";

    public string EmailRemetente { get; set; } = "contato.asfsolutions@gmail.com";

    public string NomeRemetente { get; set; } = "Lightcode";

    public bool UsarSsl { get; set; } = true;
}
