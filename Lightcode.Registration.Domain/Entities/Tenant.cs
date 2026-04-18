namespace Lightcode.Registration.Domain.Entities;

/// <summary>Registro de tenant no banco master (metadados e isolamento por database).</summary>
public class Tenant
{
    public string Id { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string DatabaseName { get; set; } = default!;

    /// <summary>Se preenchido, substitui a connection global (ex.: cluster dedicado ao cliente).</summary>
    public string? ConnectionString { get; set; }

    public bool Active { get; set; } = true;
}
