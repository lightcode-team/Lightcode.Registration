namespace Lightcode.Registration.Application.Configuration;

/// <summary>Valores usados ao criar o documento SMTP inicial na base do tenant (sobreponível via configuração).</summary>
public sealed class TenantDefaultSmtpOptions
{
    public const string SectionName = "TenantDefaultSmtp";

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    public string Usuario { get; set; } = "";

    public string Senha { get; set; } = "";

    public string EmailRemetente { get; set; } = "";

    public string NomeRemetente { get; set; } = "AFSolutions";

    public bool UsarSsl { get; set; }
}
