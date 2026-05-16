using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.SchemaConfig;

public sealed class AccountSchemaConfig
{
    public ExpiryConfig? Expiry { get; set; }

    /// <summary>2FA por email (código ou link). Chave JSON: <c>2FA</c>.</summary>
    [JsonPropertyName("2FA")]
    public EmailTwoFactorConfig? TwoFactor { get; set; }
}

public sealed class ExpiryConfig
{
    public bool ExpiryRegister { get; set; }

    public int DaysExpiry { get; set; }
}

/// <summary>Configuração de confirmação de email em duas etapas.</summary>
public sealed class EmailTwoFactorConfig
{
    public bool Active { get; set; }

    /// <summary><see cref="EmailTwoFactorType.Code"/> — o utilizador recebe um código e confirma em <c>POST /api/accounts/confirm-email-code/{code}</c>. <see cref="EmailTwoFactorType.Link"/> — o email contém um URL que confirma ao ser aberto.</summary>
    public EmailTwoFactorType? Type { get; set; }
}

/// <summary>Modo de 2FA por email.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmailTwoFactorType
{
    /// <summary>Código enviado por email; validação no endpoint de confirmação.</summary>
    Code,

    /// <summary>Link no email que aciona a API e confirma sem introduzir código.</summary>
    Link
}
