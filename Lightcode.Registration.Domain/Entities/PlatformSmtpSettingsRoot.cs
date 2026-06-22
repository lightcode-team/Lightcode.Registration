namespace Lightcode.Registration.Domain.Entities;

/// <summary>Documento na collection <c>Settings</c> da base master (<c>_id</c> = <c>smtp</c>).</summary>
public sealed class PlatformSmtpSettingsRoot
{
    public const string CollectionName = "Settings";

    public const string DocumentId = "smtp";

    public string Id { get; set; } = DocumentId;

    public TenantSmtpConfiguration Smtp { get; set; } = new();
}
