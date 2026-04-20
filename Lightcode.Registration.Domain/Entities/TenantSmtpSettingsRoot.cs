namespace Lightcode.Registration.Domain.Entities;

/// <summary>Documento na collection <c>Settings</c> da base de dados do tenant (<c>_id</c> = <c>smtp</c>).</summary>
public sealed class TenantSmtpSettingsRoot
{
    public const string CollectionName = "Settings";

    public const string DocumentId = "smtp";

    public string Id { get; set; } = DocumentId;

    public TenantSmtpConfiguration Smtp { get; set; } = new();
}

/// <summary>Bloco SMTP persistido em MongoDB (chaves alinhadas ao JSON de configuração).</summary>
public sealed class TenantSmtpConfiguration
{
    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;

    public string Usuario { get; set; } = "";

    public string Senha { get; set; } = "";

    public string EmailRemetente { get; set; } = "";

    public string NomeRemetente { get; set; } = "";

    public bool UsarSsl { get; set; }
}
