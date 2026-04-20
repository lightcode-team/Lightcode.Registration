namespace Lightcode.Registration.Domain.Entities;

/// <summary>Template de email persistido no master (<c>SaasMasterDb</c>), por tenant.</summary>
public sealed class EmailTemplate
{
    public string Id { get; set; } = default!;

    public string TenantId { get; set; } = default!;

    /// <summary>Chave única por tenant (ex.: welcome, invoice).</summary>
    public string Key { get; set; } = default!;

    public string? DisplayName { get; set; }

    public string Subject { get; set; } = default!;

    public string HtmlBody { get; set; } = default!;

    public string? TextBody { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
