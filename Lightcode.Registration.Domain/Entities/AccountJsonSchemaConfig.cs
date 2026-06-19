using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lightcode.Registration.Domain.Entities;

/// <summary>Configuração opcional do schema de conta (expiração, 2FA, validações).</summary>
public sealed class AccountJsonSchemaConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly AccountJsonSchemaConfig Empty = new();

    /// <summary>Quando false, o registo não verifica email duplicado. Aceita boolean ou string ("true"/"false").</summary>
    [JsonPropertyName("validateDuplicateEmail")]
    [JsonConverter(typeof(FlexibleBoolJsonConverter))]
    public bool ValidateDuplicateEmail { get; set; } = true;

    public ExpiryConfig? Expiry { get; set; }

    /// <summary>2FA por email (código ou link). Chave JSON: <c>2FA</c>.</summary>
    [JsonPropertyName("2FA")]
    public EmailTwoFactorConfig? TwoFactor { get; set; }

    public AccountAuthConfig? Auth { get; set; }

    public static AccountJsonSchemaConfig Parse(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return Empty;

        try
        {
            return JsonSerializer.Deserialize<AccountJsonSchemaConfig>(configJson, SerializerOptions) ?? Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public static bool TryParseAndValidate(string? configJson, out AccountJsonSchemaConfig? config, out string? error)
    {
        config = null;
        error = null;

        if (string.IsNullOrWhiteSpace(configJson))
        {
            config = Empty;
            return true;
        }

        try
        {
            config = JsonSerializer.Deserialize<AccountJsonSchemaConfig>(configJson, SerializerOptions);
        }
        catch (JsonException ex)
        {
            error = $"config JSON inválido: {ex.Message}";
            return false;
        }

        config ??= Empty;

        if (config.Expiry is not null && config.Expiry.ExpiryRegister && config.Expiry.DaysExpiry <= 0)
        {
            error = "Quando Expiry.expiryRegister é true, Expiry.daysExpiry deve ser maior que zero.";
            return false;
        }

        if (config.TwoFactor is { Active: true } && config.TwoFactor.Type is null)
        {
            error = "Quando 2FA.Active é true, 2FA.Type é obrigatório (Code ou Link).";
            return false;
        }

        if (config.Auth?.TwoFactor is { } authTwoFactor
            && !authTwoFactor.TryValidate(out error))
            return false;

        return true;
    }
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

    public EmailTwoFactorType? Type { get; set; }
}

/// <summary>Modo de 2FA por email.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmailTwoFactorType
{
    Code,
    Link
}

public sealed class AccountAuthConfig
{
    public AccountAuthTwoFactorConfig? TwoFactor { get; set; }
}

public sealed class AccountAuthTwoFactorConfig
{
    private static readonly HashSet<string> SupportedModes = new(StringComparer.OrdinalIgnoreCase)
    {
        AccountAuthTwoFactorModes.Disabled,
        AccountAuthTwoFactorModes.Optional,
        AccountAuthTwoFactorModes.Required
    };

    private static readonly HashSet<string> SupportedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        AccountAuthTwoFactorMethods.EmailCode,
        AccountAuthTwoFactorMethods.Totp
    };

    public string Mode { get; set; } = AccountAuthTwoFactorModes.Disabled;

    public IReadOnlyList<string> AllowedMethods { get; set; } = [AccountAuthTwoFactorMethods.EmailCode];

    public string DefaultMethod { get; set; } = AccountAuthTwoFactorMethods.EmailCode;

    public bool TryValidate(out string? error)
    {
        error = null;
        var mode = string.IsNullOrWhiteSpace(Mode) ? AccountAuthTwoFactorModes.Disabled : Mode.Trim();
        if (!SupportedModes.Contains(mode))
        {
            error = "auth.twoFactor.mode deve ser disabled, optional ou required.";
            return false;
        }

        var methods = AllowedMethods?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (methods.Count == 0)
        {
            error = "auth.twoFactor.allowedMethods deve conter pelo menos um método.";
            return false;
        }

        if (methods.Any(x => !SupportedMethods.Contains(x)))
        {
            error = "auth.twoFactor.allowedMethods suporta apenas email_code ou totp.";
            return false;
        }

        var defaultMethod = string.IsNullOrWhiteSpace(DefaultMethod)
            ? AccountAuthTwoFactorMethods.EmailCode
            : DefaultMethod.Trim();

        if (!SupportedMethods.Contains(defaultMethod) || !methods.Contains(defaultMethod, StringComparer.OrdinalIgnoreCase))
        {
            error = "auth.twoFactor.defaultMethod deve existir em allowedMethods.";
            return false;
        }

        return true;
    }
}

public static class AccountAuthTwoFactorModes
{
    public const string Disabled = "disabled";
    public const string Optional = "optional";
    public const string Required = "required";
}

public static class AccountAuthTwoFactorMethods
{
    public const string EmailCode = "email_code";
    public const string Totp = "totp";
}

internal sealed class FlexibleBoolJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Number => reader.TryGetInt64(out var n) ? n != 0 : reader.GetDouble() != 0,
            _ => throw new JsonException($"Valor booleano inválido: {reader.TokenType}.")
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);

    private static bool ParseString(string? value) =>
        bool.TryParse(value, out var b)
            ? b
            : string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
}
