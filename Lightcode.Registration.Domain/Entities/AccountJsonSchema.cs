namespace Lightcode.Registration.Domain.Entities;

/// <summary>Definição de JSON Schema (draft) para validação de cadastro de conta no tenant.</summary>
public class AccountJsonSchema
{
    public string Id { get; set; } = default!;

    public string TenantId { get; set; } = default!;

    /// <summary>Chave única por tenant (ex.: default, premium).</summary>
    public string Key { get; set; } = default!;

    public string? DisplayName { get; set; }

    /// <summary>JSON Schema (ex.: draft 2020-12) como texto na aplicação; no MongoDB é persistido como documento embutido.</summary>
    public string SchemaJson { get; set; } = default!;

    /// <summary>Configuração opcional (ex.: expiração de cadastro) como JSON; no MongoDB é documento embutido.</summary>
    public string? ConfigJson { get; set; }

    /// <summary>Materializa <see cref="ConfigJson"/> como objeto tipado.</summary>
    public AccountJsonSchemaConfig GetConfig() => AccountJsonSchemaConfig.Parse(ConfigJson);

    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
